// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Metadata.Cli;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal sealed record DbHash
{
    public String? Hash { get; set; }
}

/*
 * Generator of deploydatabase.sql.
 *
 * THE FILE IS A BUILD ARTIFACT, NOT A SIDE EFFECT OF THE DEPLOYMENT. It is the goal
 * here, not a log: this very file is what gets applied at the customer site, because
 * there is no other way into production. In production the metadata is compiled in and
 * the Clr provider does not enumerate it at all (EnumerateFilesRecursive => []), so the
 * runtime deployment degenerates there on its own.
 *
 * Hence the order: build the WHOLE script -> materialize it -> execute that same text.
 * Never apply it piecemeal while generating: what is executed and what is written would
 * stop being the same thing, and the developer would be debugging something other than
 * what ships.
 *
 * The cost of this design falls on the generator rather than on the process: it must be
 * deterministic (same metadata -> same bytes) and complete (everything deployed is
 * derived from the declaration). Then the file is correct by construction, and there is
 * no separate "build a release" step at all.
 */

public sealed record DeployDatabaseResult(String File, Boolean Applied);

public class SqlDbGenerator(IAppCodeProvider _appCodeProvider, IDbContext _dbContext)
{
    private const String DB_FILE = "deploydatabase.sql";

    private readonly CliDatabaseCreator _dbCreator = new();

    private String DatabaseFilePath => _appCodeProvider.GetMainModuleFullPath("_sqlscripts", DB_FILE);

    private String DeployFile => DatabaseFilePath.NormalizeSlash();

    public async Task<DeployDatabaseResult> CheckDeployAsync(String? dataSource, IEnumerable<TableMetadata> tables, AppPlatformId platformId)
    {
        if (await WriteDeployAsync(dataSource, tables, platformId) is not var (script, hash))
            return new DeployDatabaseResult(DeployFile, false);

        // DEPLOY DATABASE
        // Running it here verifies the artifact against a live database;
        // it is not a separate deployment path.
        await DeployDatabaseAsync(dataSource, DatabaseFilePath, script);

        // save hash
        await _dbContext.ExecuteAsync<DbHash>(dataSource, "a2meta.[SetDbHash]", new DbHash() { Hash = hash });
        return new DeployDatabaseResult(DeployFile, true);
    }

    /* Generate and write deploydatabase.sql, without executing it. On its own - for
     * 'a2 meta deploy --full', where the file is executed inside full.sql and the hash is left to the
     * next plain deploy: that one sees the mismatch, runs the idempotent script again and writes it.
     * null - the hash matched: the database got this very file already, and it lies on disk.
     */
    public async Task<(String Script, String Hash)?> WriteDeployAsync(String? dataSource, IEnumerable<TableMetadata> tables, AppPlatformId platformId)
    {
        var seedScript = await GenerateMetadataSeedAsync(tables);
        var seedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seedScript ?? "new"))).ToLowerInvariant();
        // read hash from DB
        var dbHash = await _dbContext.LoadAsync<DbHash>(dataSource, "a2meta.[GetDbHash]");

        if (dbHash?.Hash == seedHash)
            return null;

        var allScript = new StringBuilder();

        // CREATE SCRIPT
        // The order is forced from both sides: SyncSchema needs the tables to already
        // exist (step above), and must run BEFORE the foreign keys, since those
        // reference columns it has just added.
        allScript.AppendLine(seedScript);
        allScript.AppendLine(CreatePlatformIdScript(platformId));
        allScript.AppendLine(CreateSchemasScript(tables));
        allScript.AppendLine(CreateTablesScript(tables, platformId));
        allScript.AppendLine(CreateTableTypesScript(tables));
        allScript.AppendLine(SyncSchemaScript());
        /* After SyncSchema, because a column added to the shape of a set is written by this merge;
         * before the foreign keys, because 'alter table add constraint foreign key' validates the
         * rows that are already there - a document on a code the set does not carry yet would
         * fail the deploy instead of being corrected by it.
         */
        allScript.AppendLine(CreateSetValuesScript(tables));
        allScript.AppendLine(CreateAutonumsScript(tables));
        allScript.AppendLine(CreateSeedScript(tables));
        allScript.AppendLine(CreateOperationsScript(tables));
        allScript.AppendLine(CreateAutonumProcedureScript(tables));
        allScript.AppendLine(SystemUserScript());
        allScript.AppendLine(CreateForeignKeysScript(tables));
        allScript.AppendLine(CreateIndexesScript(tables));

        // Materialize first, execute second - and execute exactly the same text.
        var script = allScript.ToString();
        await WriteDeployDatabaseFileAsync(script);
        return (script, seedHash);
    }

    private Task WriteDeployDatabaseFileAsync(String allScript)
    {
        var dbPath = DatabaseFilePath;
        var dbDir = Path.GetDirectoryName(dbPath);
        if (String.IsNullOrWhiteSpace(dbDir))
            throw new InvalidOperationException($"Invalid path '{dbPath}'");
        Directory.CreateDirectory(dbDir);
        return File.WriteAllTextAsync(dbPath, allScript, Encoding.UTF8);
    }

    /* The system user: who wrote a row no person wrote - the default of every stamp. Before the
     * foreign keys, which validate the rows already there.
     *
     * Here and not in the platform script, for its order. The stamps' keys need a2security.Users to
     * exist, so by now that script has run and seeded its administrator - under 'the table is
     * empty', a guard a row inserted any earlier would have silently turned off. ViewUsers already
     * leaves Id 0 out, so it is never a login.
     */
    private static String SystemUserScript() => """
        -- SYSTEM USER
        if not exists(select * from a2security.Users where Id = 0)
            insert into a2security.Users(Id, UserName, SecurityStamp) values (0, N'System', N'');
        go

        """;

    private static String SyncSchemaScript()
    {
        return $"""
        -- SYNC DATABASE SCHEMA
        exec a2meta.[SyncSchema]
        go

        """;
    }

    private String CreatePlatformIdScript(AppPlatformId platformId)
    {
        return $"""

        -- PLATFORM ID TYPE
        {CliDatabaseCreator.SQL_DIVIDER}
        if type_id(N'dbo.platformid') is null
        	create type dbo.platformid from {platformId.SqlTypeName};
        go        
        """;
    }

    private String CreateSchemasScript(IEnumerable<TableMetadata> tables)
    {
        var schemas = tables.GroupBy(t => t.SqlSchema).Select(g => g.Key).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("-- SCHEMAS");
        sb.AppendLine(CliDatabaseCreator.SQL_DIVIDER);
        foreach (var s in schemas)
            sb.AppendLine($"""
            if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'{s}')
            	exec sp_executesql N'create schema {s} authorization dbo';
            go
            """);
        sb.AppendLine();
        foreach (var s in schemas)
            sb.AppendLine($"grant select, insert, update, execute on schema::{s} to public;");
        sb.AppendLine("go");
        return sb.ToString();
    }

    /* Every table the deploy touches: the ones declared in files, and the satellites the platform
     * adds to them - tag entries, autonum counters, the rows of a collection. Four steps walk this
     * (tables, seed, foreign keys, indexes) and they used to walk it four times, agreeing only
     * because someone kept them equal.
     *
     * The master travels with the table because a foreign key needs it and nothing else does - a
     * master column carries no target of its own, so this walk is the only place that knows what
     * it points at. Cheaper to hand it out here than to have a second walk repeat the same three
     * rules to find out what hangs under what.
     *
     * The tags catalog is nobody's satellite - one table for the whole application - so it comes
     * once, after the walk, and only if anything is tagged.
     */
    private static IEnumerable<(TableMetadata Table, TableMetadata? Master)> DeployTables(IEnumerable<TableMetadata> tables)
    {
        foreach (var table in tables)
        {
            yield return (table, null);
            if (table.HasTags)
                yield return (TableMetadataDefaults.CreateTagEntriesTable(table), table);
            if (table.Kind == EndpointKind.Autonum)
                yield return (TableMetadataDefaults.CreateAutonumValuesTable(table), table);
            foreach (var d in table.Details.Values)
                yield return (d, table);
        }
        if (tables.Any(t => t.HasTags))
            yield return (TableMetadataDefaults.TagsTable(), null);
    }

    private String CreateTablesScript(IEnumerable<TableMetadata> tables, AppPlatformId platformId)
    {
        var strBuilder = new StringBuilder();
        strBuilder.AppendLine("-- TABLES");
        foreach (var (table, _) in DeployTables(tables))
        {
            strBuilder.AppendLine(_dbCreator.CreateTable(table, platformId));
            strBuilder.AppendLine("go");
        }
        return strBuilder.ToString();
    }

    /* The rows of every declared set. Not part of the metadata seed even though it is declaration:
     * the seed runs first, before the tables exist, and it answers 'what is the schema'. What makes
     * a changed list of values reach the database at all is the fingerprint the seed carries for the
     * table (TableMetadata.Xtra) - without it the hash would match and this script would never run.
     *
     * A value that disappears from the file is WITHDRAWN, not erased: records already point at it,
     * and 'no longer choosable' is exactly what Void says. So dropping a value from the file and
     * writing 'void': true mean the same thing on the database side. Deleting would fail on the
     * foreign key wherever the value is used, and lose the name of the code wherever it is not.
     *
     * The 'All' row is added here and never declared: its key is the empty string, it means 'do not
     * restrict', and that is a state of a filter rather than a value a record can hold.
     *
     * The column list is the TABLE's, not a list written here: a set of states carries two columns
     * an enum does not, and a merge spelled out by hand would write the rows and leave those two
     * null - silently, since they are nullable. Every column is answered for by name, and a column
     * with no answer throws: the baseline of a set and this statement are one thing said twice, and
     * the second saying must fail loudly rather than fall through to a default.
     */
    // internal, not private: the script IS the artifact (see the header), and nothing else can read it
    internal static String CreateSetValuesScript(IEnumerable<TableMetadata> tables)
    {
        var sets = tables.Where(t => t.IsSet && t.Values.Count > 0).ToList();
        if (sets.Count == 0)
            return String.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("-- SET VALUES");
        foreach (var e in sets)
        {
            var columns = e.AllColumns().ToList();

            /* What one column of one value holds. The 'All' row is the same walk with no value:
             * its name is the set's own key, its order puts it first, and everything else is null -
             * a role least of all, because it is not a state.
             */
            String Cell(TableColumn column, SetValueMetadata? value, Int32 order)
            {
                var text = column.Name switch
                {
                    Constants.FieldNames.Id => value?.Id ?? String.Empty,
                    Constants.FieldNames.Name => value == null
                        ? $"@[{e.Model}.All]"
                        : value.Name ?? $"@[{e.Model}.{value.Id}]",
                    Constants.FieldNames.Memo => value?.Memo,
                    Constants.FieldNames.Order => order.ToString(),
                    Constants.FieldNames.Void => value != null && value.Void ? "1" : "0",
                    /* The 'All' row is drawn by the same picker as the rest, so it needs a colour
                     * of its own: with none it renders as nothing at all, and an empty badge reads
                     * as a control that failed rather than as 'no filter'. White is the platform's
                     * answer and comes from the vocabulary the load checks against, so the row it
                     * writes is a row an author could have written. A declared value with no colour
                     * keeps none - that one is a choice, and it draws as a plain label.
                     */
                    Constants.FieldNames.Color => value == null
                        ? nameof(TagLabelStyle.White).ToLowerInvariant()
                        : value.Color,
                    Constants.FieldNames.Role => value?.Role?.ToString(),
                    _ => throw new InvalidOperationException(
                        $"{e.Path}: nothing to write into '{column.Name}' - a column of a set that a value does not answer for")
                };
                return text == null ? "null" : column.SqlLiteral(text);
            }

            String Row(SetValueMetadata? value, Int32 order) =>
                $"\t({String.Join(", ", columns.Select(c => Cell(c, value, order)))})";

            var rows = new List<String> { Row(null, -1) };
            rows.AddRange(e.Values.Select((v, ix) => Row(v, ix)));

            var key = e.KeyColumn.Name;
            var names = String.Join(", ", columns.Select(c => $"[{c.Name}]"));
            var sources = String.Join(", ", columns.Select(c => $"s.[{c.Name}]"));
            // the continuation lines carry their own indent: an interpolation is not re-indented
            var updates = String.Join($",{Environment.NewLine}        ",
                columns.Where(c => !c.IsKey).Select(c => $"t.[{c.Name}] = s.[{c.Name}]"));

            sb.AppendLine($"""
            {CliDatabaseCreator.SQL_DIVIDER}
            begin
                set nocount on;
                declare @{e.Model} table({String.Join(", ", columns.Select(c => $"[{c.Name}] {c.SqlDataType()}"))});

                insert into @{e.Model}({names}) values
            {String.Join($",{Environment.NewLine}", rows)};

                merge {e.SqlTableName} as t
                using @{e.Model} as s
                on t.[{key}] = s.[{key}]
                when matched then update set
                    {updates}
                when not matched then insert ({names}) values
                    ({sources})
                when not matched by source then update set
                    t.[{Constants.FieldNames.Void}] = 1;
            end
            go
            """);
        }
        return sb.ToString();
    }

    /* The declared numberings, merged by key. Neither arm the set values script has for a row that left
     * the file: nothing withdraws a numbering (no 'void' - nobody picks one at run time) and
     * nothing deletes it, because its counters outlive it and documents carry the numbers it
     * issued. The price is a registry that only grows; the row is two hundred bytes and the
     * alternative loses the meaning of numbers already printed.
     */
    private static String CreateAutonumsScript(IEnumerable<TableMetadata> tables)
    {
        static String Str(String? val) =>
            val == null ? "null" : $"N'{val.Replace("'", "''")}'";

        var registries = tables.Where(t => t.Kind == EndpointKind.Autonum && t.Autonums.Count > 0).ToList();
        if (registries.Count == 0)
            return String.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("-- AUTONUMS");
        foreach (var reg in registries)
        {
            var rows = reg.Autonums.Select(a =>
                $"\t({Str(a.Id)}, {Str(a.Name ?? $"@[{reg.Model}.{a.Id}]")}, {Str(a.Pattern)}, {Str(a.Period.ToString())})");

            sb.AppendLine($"""
            {CliDatabaseCreator.SQL_DIVIDER}
            begin
                set nocount on;
                declare @{reg.Model} table([Id] {reg.KeyColumn.SqlDataType()}, [Name] nvarchar(255),
                    [Pattern] nvarchar(255), [Period] nvarchar(16));

                insert into @{reg.Model}([Id], [Name], [Pattern], [Period]) values
            {String.Join($",{Environment.NewLine}", rows)};

                merge {reg.SqlTableName} as t
                using @{reg.Model} as s
                on t.[Id] = s.[Id]
                when matched then update set
                    t.[Name] = s.[Name],
                    t.[Pattern] = s.[Pattern],
                    t.[Period] = s.[Period]
                when not matched then insert ([Id], [Name], [Pattern], [Period]) values
                    (s.[Id], s.[Name], s.[Pattern], s.[Period]);
            end
            go
            """);
        }
        return sb.ToString();
    }

    /* The rows of a chart of accounts, merged from its seed file. A dumb merge, and every arm of it
     * is a rule of the chart:
     * - a row of the file is the configuration: IsSystem = 1, Void = 0, the columns the file names;
     * - a column a row does not name is left as it is - an author field is written only where the
     *   row speaks about it, so each one travels with a '$'-bit saying whether it was named;
     * - a row of the file that has left it keeps its row (the postings hold it by FK) and becomes
     *   the user's: IsSystem = 0, closed for choosing. Void = 0 would leave an account the code gave
     *   up silently available. Put back into the file, the same merge restores it.
     * A row with IsSystem = 0 is the user's and the merge never touches it.
     *
     * Emitted for a chart with no rows as well: emptying the file is the last arm for every row.
     */
    private static String CreateSeedScript(IEnumerable<TableMetadata> tables)
    {
        static String Str(String? val) =>
            val == null ? "null" : $"N'{val.Replace("'", "''")}'";

        var charts = tables.Where(t => t.Kind == EndpointKind.AccPlan).ToList();
        if (charts.Count == 0)
            return String.Empty;

        String[] baseline = [Constants.FieldNames.Id, Constants.FieldNames.Name, Constants.FieldNames.Parent,
            Constants.FieldNames.AccountType, Constants.FieldNames.NormalBalance];

        var sb = new StringBuilder();
        sb.AppendLine("-- SEED");
        foreach (var chart in charts)
        {
            var columns = chart.AllColumns().ToDictionary(c => c.Name);
            // in declaration order, only those some row names
            var authored = chart.Columns
                .Where(c => chart.SeedRows.Any(r => r.Values.ContainsKey(c.Name)))
                .ToList();

            var declared = baseline.Select(n => $"[{n}] {columns[n].SqlDataType()}")
                .Concat(authored.Select(c => $"[{c.Name}] {c.SqlDataType()}, [${c.Name}] bit"));

            // the key is outside the row's values, as it is outside a row of the file
            String Value(SeedRow row, String name) =>
                name == Constants.FieldNames.Id ? Str(row.Id)
                : row.Values.GetValueOrDefault(name) is String v ? columns[name].SqlLiteral(v) : "null";

            var rows = chart.SeedRows.Select(r => $"\t({String.Join(", ",
                baseline.Select(n => Value(r, n))
                    .Concat(authored.SelectMany(c => new[] { Value(r, c.Name), r.Values.ContainsKey(c.Name) ? "1" : "0" })))})");

            var names = baseline.Select(n => $"[{n}]")
                .Concat(authored.SelectMany(c => new[] { $"[{c.Name}]", $"[${c.Name}]" }));

            var insert = chart.SeedRows.Count == 0 ? String.Empty : $"""
                insert into @{chart.Model}({String.Join(", ", names)}) values
            {String.Join($",{Environment.NewLine}", rows)};

            """;

            var updated = baseline.Where(n => n != Constants.FieldNames.Id).Select(n => $"t.[{n}] = s.[{n}]")
                .Concat(authored.Select(c => $"t.[{c.Name}] = case when s.[${c.Name}] = 1 then s.[{c.Name}] else t.[{c.Name}] end"))
                .Concat([$"t.[{Constants.FieldNames.IsSystem}] = 1", $"t.[{Constants.FieldNames.Void}] = 0"]);
            var inserted = baseline.Concat(authored.Select(c => c.Name))
                .Concat([Constants.FieldNames.IsSystem, Constants.FieldNames.Void]);
            var insertedValues = baseline.Concat(authored.Select(c => c.Name)).Select(n => $"s.[{n}]")
                .Concat(["1", "0"]);

            sb.AppendLine($"""
            {CliDatabaseCreator.SQL_DIVIDER}
            begin
                set nocount on;
                declare @{chart.Model} table({String.Join(", ", declared)});

            {insert}    merge {chart.SqlTableName} as t
                using @{chart.Model} as s
                on t.[Id] = s.[Id]
                when matched then update set
                    {String.Join($",{Environment.NewLine}        ", updated)}
                when not matched then insert ({String.Join(", ", inserted.Select(n => $"[{n}]"))}) values
                    ({String.Join(", ", insertedValues)})
                when not matched by source and t.[{Constants.FieldNames.IsSystem}] = 1 then update set
                    t.[{Constants.FieldNames.IsSystem}] = 0,
                    t.[{Constants.FieldNames.Void}] = 1;
            end
            go
            """);
        }
        return sb.ToString();
    }

    /* The operations of the document families - the rows the documents' Operation column points at,
     * so before the foreign keys. Shaped as the numbering merge: an operation dropped from the files
     * keeps its row, documents carry its code. The name is the localization key, as a set's is.
     */
    private static String CreateOperationsScript(IEnumerable<TableMetadata> tables)
    {
        static String Str(String? val) =>
            val == null ? "null" : $"N'{val.Replace("'", "''")}'";

        var registry = tables.FirstOrDefault(t => t.Operations.Count > 0);
        if (registry == null)
            return String.Empty;

        var rows = registry.Operations.Select(o =>
            $"\t({Str(o.Id)}, {Str($"@[{registry.Model}.{o.Id}]")})");

        return $"""
            -- OPERATIONS
            {CliDatabaseCreator.SQL_DIVIDER}
            begin
                set nocount on;
                declare @{registry.Model} table([Id] {registry.KeyColumn.SqlDataType()}, [Name] nvarchar(255));

                insert into @{registry.Model}([Id], [Name]) values
            {String.Join($",{Environment.NewLine}", rows)};

                merge {registry.SqlTableName} as t
                using @{registry.Model} as s
                on t.[Id] = s.[Id]
                when matched then update set
                    t.[Name] = s.[Name]
                when not matched then insert ([Id], [Name]) values
                    (s.[Id], s.[Name]);
            end
            go

            """;
    }

    /* The one place a number is issued. A procedure and not inline SQL in every save: the pattern
     * is substituted in a dozen statements, and repeated per numbered endpoint it would be a dozen
     * chances for two of them to differ.
     *
     * Two ports, @Autonum and @Date, and a number out - the document's own date, because a number
     * belongs to the period the document is IN, not to the moment it was typed.
     *
     * The counter is advanced by one statement and is committed with it, before the save that
     * asked continues. That is what makes gaps normal here - a save that fails afterwards has
     * already spent the number - and it is the deliberate choice, see CLAUDE.md, "Autonums".
     */
    private static String CreateAutonumProcedureScript(IEnumerable<TableMetadata> tables)
    {
        var registry = tables.FirstOrDefault(t => t.Kind == EndpointKind.Autonum);
        if (registry == null)
            return String.Empty;
        var values = TableMetadataDefaults.CreateAutonumValuesTable(registry);

        // '$$' so that a single brace is content: the body is full of '{yyyy}' and two of them are
        // scanned for at run time, where doubling them would be a bug that only shows in output
        return $$"""
        -- AUTONUM
        {{CliDatabaseCreator.SQL_DIVIDER}}
        create or alter procedure {{TableMetadataDefaults.AutonumProcedureName()}}
        @Autonum {{registry.KeyColumn.SqlDataType()}},
        @Date date,
        @Number nvarchar(64) output
        as
        begin
            set nocount on;
            set transaction isolation level read committed;

            declare @pattern nvarchar(255), @y int, @q int, @m int;

            select @pattern = [Pattern],
                @y = case when [Period] <> N'{{AutonumPeriod.None}}' then year(@Date) else 0 end,
                @q = case when [Period] = N'{{AutonumPeriod.Quarter}}' then datepart(quarter, @Date) else 0 end,
                @m = case when [Period] = N'{{AutonumPeriod.Month}}' then month(@Date) else 0 end
            from {{registry.SqlTableName}} where [Id] = @Autonum;

            /* Not a 'UI:' message: nothing here is the user's to fix, and the load refuses a
             * numbering that is not declared (CheckAutonumDeclaredAsync), so what is left to reach
             * this is a database behind its own metadata. That reader needs the key and the table,
             * which is exactly what a localized string cannot carry.
             */
            if @pattern is null
            begin
                declare @msg nvarchar(255) = concat(N'Autonum ''', @Autonum, N''' is not found in {{registry.SqlTableName}}. Redeploy the database');
                throw 60000, @msg, 0;
            end

            /* One statement, and 'holdlock' is what makes it one: update-then-insert lets two
             * callers both find no row for a period that has just begun and both insert one - a
             * counter split in two, and from then on every number issued twice. The lock is taken
             * over the table's unique index, so it is on that one key and not on a range of them.
             */
            declare @rtable table(number int);
            merge into {{values.SqlTableName}} with (holdlock) as t
            using (select @Autonum, @y, @q, @m) as s([Autonum], [Year], [Quart], [Month])
                on t.[Autonum] = s.[Autonum] and t.[Year] = s.[Year]
                    and t.[Quart] = s.[Quart] and t.[Month] = s.[Month]
            when matched then update set t.[CurrentNumber] = t.[CurrentNumber] + 1
            when not matched then insert ([Autonum], [Year], [Quart], [Month], [CurrentNumber])
                values (s.[Autonum], s.[Year], s.[Quart], s.[Month], 1)
            output inserted.[CurrentNumber] into @rtable(number);

            declare @n int;
            select @n = number from @rtable;

            set @Number = replace(@pattern, N'{yyyy}', format(@Date, N'yyyy'));
            set @Number = replace(@Number, N'{yy}', format(@Date, N'yy'));
            set @Number = replace(@Number, N'{mm}', format(@Date, N'MM'));
            set @Number = replace(@Number, N'{qq}', format(datepart(quarter, @Date), N'00'));

            -- the counter's own token carries its width: '{nnnnn}' is five digits, zero padded
            declare @p0 int, @p1 int;
            set @p0 = charindex(N'{n', @Number);
            set @p1 = charindex(N'n}', @Number);
            set @Number = stuff(@Number, @p0, @p1 - @p0 + 2, format(@n, replicate(N'0', @p1 - @p0)));
        end
        go

        """;
    }

    // Last, so a unique index meets the rows already in the table

    private static String CreateIndexesScript(IEnumerable<TableMetadata> tables)
    {
        var scripts = DeployTables(tables).Select(t => CliDatabaseCreator.CreateIndexes(t.Table))
            .Where(s => !String.IsNullOrWhiteSpace(s))
            .ToList();
        if (scripts.Count == 0)
            return String.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("-- INDEXES");
        foreach (var script in scripts)
        {
            sb.AppendLine(script);
            sb.AppendLine("go");
        }
        return sb.ToString();
    }

    private static String CreateTableTypesScript(IEnumerable<TableMetadata> tables)
    {
        static Boolean HasTableType(TableMetadata table)
            => !table.IsJournal && !table.IsLedger && !table.IsTags && !table.IsTagEntries;

        var strBuilder = new StringBuilder();
        strBuilder.AppendLine("-- TABLE TYPES");
        strBuilder.AppendLine(CliDatabaseCreator.CreateIdTableType());
        strBuilder.AppendLine("go");
        foreach (var table in tables.Where(HasTableType))
        {
            strBuilder.AppendLine(CliDatabaseCreator.CreateTableType(table));
            strBuilder.AppendLine("go");
            foreach (var d in table.Details)
            {
                strBuilder.AppendLine(CliDatabaseCreator.CreateTableType(d.Value));
                strBuilder.AppendLine("go");
            }
        }
        if (tables.Any(t => t.HasTags))
        {
            var tagsTable = TableMetadataDefaults.TagsTable();
            strBuilder.AppendLine(CliDatabaseCreator.CreateTableType(tagsTable));
            strBuilder.AppendLine("go");
        }
        return strBuilder.ToString();
    }

    private static String CreateForeignKeysScript(IEnumerable<TableMetadata> tables)
    {
        var strBuilder = new StringBuilder();
        strBuilder.AppendLine("-- FOREIGN KEYS");
        // the master -> the tagged table for tag entries, the header for a detail: the walk hands
        // it out, which is the only reason it carries one.
        foreach (var (table, master) in DeployTables(tables))
        {
            var fc = CliDatabaseCreator.CreateForeignKeys(table, master);
            if (!String.IsNullOrWhiteSpace(fc))
            {
                strBuilder.AppendLine(fc);
                strBuilder.AppendLine("go");
            }
        }
        return strBuilder.ToString();
    }

    /* The seed is the canonical representation of the schema, not just another chunk of
     * the script: the rest of the file (tables, types, foreign keys) is derived from the
     * same declaration. That is why the hash is taken from it - if it matches, there is
     * nothing to deploy and nothing to rebuild.
     * The invariant to keep: everything the deployment learns to do (indexes, CHECK
     * constraints, computed columns) must also make it into the seed. Otherwise the
     * change is there while the hash stays the same.
     *
     * And that is why the hash is NOT taken from the whole generated script, however
     * tempting that looks as a way to keep the invariant automatically. The hash has to be
     * a function of the DECLARATION: over the script it would answer 'did the generator's
     * output change' instead, so a reformatting in a platform release would re-run a full
     * deploy on every customer database - and CreatePlatformIdScript depends on the base the
     * database runs on, so one declaration would even hash differently on two of them.
     * A new fact reaches the hash by being written into the seed - see the 'xtra'
     * fingerprint of a table, which is what carries the declared values of a set.
     */

    private static readonly Version assVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new();

    private static async Task<String?> GenerateMetadataSeedAsync(IEnumerable<TableMetadata> tables)
    {
        if (!tables.Any())
            return null;
        var sqlTables = new List<String>();
        var sqlColumns = new List<String>();

        var version = $"{assVersion.Major}.{assVersion.Minor}.{assVersion.Build}";

        static String Str(String? val) =>
            val == null ? "null" : $"N'{val.Replace("'", "''")}'";
        static String Num(Int32? val) =>
            val?.ToString() ?? "null";

        static String ColumnRow(TableMetadata table, TableColumn col)
        {
            // a self link has no RefTable - it points at this table, as its foreign key does (CreateForeignKeys)
            var refTable = col.Type == ColumnType.Parent ? table
                : col.IsRef ? col.RefTable?.Storage : null;
            // one descriptor, four facets - this row is exactly where they must agree
            var ti = col.ToSqlDbTypeInfo();
            return $"\t({Str(table.SqlSchema)}, {Str(table.Table)}, {Str(col.Name)}, {Str(ti.SqlName)}, " +
                $"{Num(ti.Length)}, {Num(ti.Precision)}, {Num(ti.Scale)}, " +
                $"{(col.DeployNullable() ? 1 : 0)}, {Str(refTable?.SqlSchema)}, {Str(refTable?.Table)}, " +
                $"{Str(col.DeployDefault())})";
        }

        /* The master is the one the foreign keys are built from - same walk, so the seed cannot
         * disagree with the DDL. Written only where a link column exists: the walk hands a master
         * to the autonum counters too, and those are keyed by a code with no link at all.
         */
        void AddTable(TableMetadata t, TableMetadata? master)
        {
            var m = String.IsNullOrEmpty(t.MasterField) ? null : master;
            sqlTables.Add($"\t({Str(t.SqlSchema)}, {Str(t.Table)}, {Str(t.Xtra())}, " +
                $"{Str(m?.SqlSchema)}, {Str(m?.Table)}, {Str(m == null ? null : t.MasterField)})");
            foreach (var col in t.AllColumns())
                sqlColumns.Add(ColumnRow(t, col));
        }

        foreach (var (t, master) in DeployTables(tables))
            AddTable(t, master);

        // the hash is taken from the text, so the order must not depend on how the
        // file system happens to be enumerated
        sqlTables.Sort(StringComparer.Ordinal);
        sqlColumns.Sort(StringComparer.Ordinal);

        var rowDiv = $",{Environment.NewLine}\t";
        var sqlScript = $"""
        /* METADATA SEED. Version: {version} */
        begin
            set nocount on;
            declare @tables table([schema] sysname, [table] sysname, [xtra] nvarchar(64),
                [master_schema] nvarchar(128), [master_table] nvarchar(128),
                [master_column] nvarchar(128));
            declare @columns table([schema] sysname, [table] sysname, [column] sysname, [datatype] sysname,
                [length] int, [precision] tinyint, [scale] tinyint, [nullable] bit,
                [ref_schema] nvarchar(128), [ref_table] nvarchar(128), [default] nvarchar(128));

            insert into @tables([schema], [table], [xtra],
                [master_schema], [master_table], [master_column]) values
            {String.Join(rowDiv, sqlTables)};

            insert into @columns([schema], [table], [column], [datatype],
                [length], [precision], [scale], [nullable], [ref_schema], [ref_table], [default]) values
            {String.Join(rowDiv, sqlColumns)};

            -- merge tables
            merge a2meta.Tables as t
            using @tables as s
            on t.[schema] = s.[schema] and t.[table] = s.[table]
            when matched then update set
                t.[xtra] = s.[xtra],
                t.[master_schema] = s.[master_schema],
                t.[master_table] = s.[master_table],
                t.[master_column] = s.[master_column]
            when not matched then insert([schema], [table], [xtra],
                [master_schema], [master_table], [master_column]) values
               (s.[schema], s.[table], s.[xtra],
                s.[master_schema], s.[master_table], s.[master_column])
            when not matched by source then delete;

            -- merge columns
            merge a2meta.Columns as t
            using @columns as s
            on t.[schema] = s.[schema] and t.[table] = s.[table] and t.[column] = s.[column]
            when matched then update set
                t.[datatype] = s.[datatype],
                t.[length] = s.[length],
                t.[precision] = s.[precision],
                t.[scale] = s.[scale],
                t.[nullable] = s.[nullable],
                t.[ref_schema] = s.[ref_schema],
                t.[ref_table] = s.[ref_table],
                t.[default] = s.[default]
            when not matched then insert
                ([schema], [table], [column], [datatype],
                 [length], [precision], [scale], [nullable], [ref_schema], [ref_table], [default]) values
                (s.[schema], s.[table], s.[column], s.[datatype],
                 s.[length], s.[precision], s.[scale], s.[nullable], s.[ref_schema], s.[ref_table], s.[default])
            when not matched by source then delete;
        end
        go
        """;

        return sqlScript;
    }

    /* 'go' alone on its line, in any case and with any line ending - as SSMS reads it. The generated
     * file has CRLF, but app.sql and full.sql carry scripts written by hand, and those come with LF too.
     * The line break before 'go' stays in the batch above it, so the count of lines below holds.
     */
    private static readonly Regex _goLine = new(@"^[ \t]*go[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // 'file' is where allScript lies on disk: the coordinates of a failure are lines in it
    public async Task DeployDatabaseAsync(String? dataSource, String file, String allScript)
    {
        if (String.IsNullOrWhiteSpace(allScript))
            return;
        var scripts = _goLine.Split(allScript);

        using var dbConn = await _dbContext.GetDbConnectionAsync(dataSource);
        using var cmd = dbConn.CreateCommand() as SqlCommand
            ?? throw new InvalidOperationException("Invalid Database provider");

        Int32 lineFrom = 1;
        Int32 lineTo = 1;
        try
        {
            foreach (var line in scripts)
            {
                lineTo = lineFrom + line.Count(c => c == '\n');
                cmd.CommandText = line;
                await cmd.ExecuteNonQueryAsync();
                lineFrom = lineTo; // the 'go' line: the break that ends it opens the next batch
            }
        }
        catch (Exception ex)
        {
            throw new DeployScriptException(ex, file, lineFrom, lineTo);
        }
    }
}

// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Metadata.Cli;

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

    public async Task<DeployDatabaseResult> CheckDeployAsync(String? dataSource, IEnumerable<TableMetadata> tables, AppPlatformId platformId)
    {
        var seedScript = await GenerateMetadataSeedAsync(tables);
        var seedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seedScript ?? "new"))).ToLowerInvariant();
        // read hash from DB
        var dbHash = await _dbContext.LoadAsync<DbHash>(dataSource, "a2meta.[GetDbHash]");

        if (dbHash?.Hash == seedHash)
            return new DeployDatabaseResult(DatabaseFilePath.NormalizeSlash(), false);

        var allScript = new StringBuilder();

        // CREATE SCRIPT
        // The order is forced from both sides: SyncSchema needs the tables to already
        // exist (step above), and must run BEFORE the foreign keys, since those
        // reference columns it has just added.
        allScript.AppendLine(seedScript);
        allScript.AppendLine(CreatePlatformIdScript(platformId));
        allScript.AppendLine(CreateSchemasScript(tables));
        allScript.AppendLine(CreateTablesScript(tables));
        allScript.AppendLine(CreateTableTypesScript(tables));
        allScript.AppendLine(SyncSchemaScript());
        /* After SyncSchema, because a column added to the shape of a set is written by this merge;
         * before the foreign keys, because 'alter table add constraint foreign key' validates the
         * rows that are already there - a document on a code the set does not carry yet would
         * fail the deploy instead of being corrected by it.
         */
        allScript.AppendLine(CreateEnumValuesScript(tables));
        allScript.AppendLine(CreateAutonumsScript(tables));
        allScript.AppendLine(CreateAutonumProcedureScript(tables));
        allScript.AppendLine(CreateForeignKeysScript(tables));
        allScript.AppendLine(CreateIndexesScript(tables));

        // Materialize first, execute second - and execute exactly the same text.
        await WriteDeployDatabaseFileAsync(allScript.ToString());

        // DEPLOY DATABASE
        // Running it here verifies the artifact against a live database;
        // it is not a separate deployment path.
        await DeployDatabaseAsync(dataSource, allScript.ToString());

        // save hash
        await _dbContext.ExecuteAsync<DbHash>(dataSource, "a2meta.[SetDbHash]", new DbHash() { Hash = seedHash });
        return new DeployDatabaseResult(DatabaseFilePath.NormalizeSlash(), true);
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
     * The owner travels with the table because a foreign key needs it and nothing else does. It is
     * cheaper to hand it out here than to have a second walk repeat the same three rules to find
     * out who owns what.
     *
     * The tags catalog is nobody's satellite - one table for the whole application - so it comes
     * once, after the walk, and only if anything is tagged.
     */
    private static IEnumerable<(TableMetadata Table, TableMetadata? Owner)> DeployTables(IEnumerable<TableMetadata> tables)
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

    private String CreateTablesScript(IEnumerable<TableMetadata> tables)
    {
        var strBuilder = new StringBuilder();
        strBuilder.AppendLine("-- TABLES");
        foreach (var (table, _) in DeployTables(tables))
        {
            strBuilder.AppendLine(_dbCreator.CreateTable(table));
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
     */
    private static String CreateEnumValuesScript(IEnumerable<TableMetadata> tables)
    {
        static String Str(String? val) =>
            val == null ? "null" : $"N'{val.Replace("'", "''")}'";

        var enums = tables.Where(t => t.Kind == EndpointKind.Enum && t.Values.Count > 0).ToList();
        if (enums.Count == 0)
            return String.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("-- ENUM VALUES");
        foreach (var e in enums)
        {
            var rows = new List<String>
            {
                $"\t({Str(String.Empty)}, {Str($"@[{e.Model}.All]")}, null, -1, 0)"
            };
            rows.AddRange(e.Values.Select((v, ix) =>
                $"\t({Str(v.Id)}, {Str(v.Name ?? $"@[{e.Model}.{v.Id}]")}, {Str(v.Memo)}, {ix}, {(v.Void ? 1 : 0)})"));

            sb.AppendLine($"""
            {CliDatabaseCreator.SQL_DIVIDER}
            begin
                set nocount on;
                declare @{e.Model} table([Id] nvarchar(64), [Name] nvarchar(255), [Memo] nvarchar(255),
                    [Order] int, [Void] bit);

                insert into @{e.Model}([Id], [Name], [Memo], [Order], [Void]) values
            {String.Join($",{Environment.NewLine}", rows)};

                merge {e.SqlTableName} as t
                using @{e.Model} as s
                on t.[Id] = s.[Id]
                when matched then update set
                    t.[Name] = s.[Name],
                    t.[Memo] = s.[Memo],
                    t.[Order] = s.[Order],
                    t.[Void] = s.[Void]
                when not matched then insert ([Id], [Name], [Memo], [Order], [Void]) values
                    (s.[Id], s.[Name], s.[Memo], s.[Order], s.[Void])
                when not matched by source then update set
                    t.[Void] = 1;
            end
            go
            """);
        }
        return sb.ToString();
    }

    /* The declared numberings, merged by key. Neither arm the enum script has for a row that left
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
                declare @{reg.Model} table([Id] nvarchar(64), [Name] nvarchar(255),
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
        @Autonum nvarchar(64),
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
            => !table.IsJournal && !table.IsTags && !table.IsTagEntries;

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
        // Owner -> the tagged table for tag entries, the header for a detail: the walk hands it
        // out, which is the only reason it carries one.
        foreach (var (table, owner) in DeployTables(tables))
        {
            var fc = CliDatabaseCreator.CreateForeignKeys(table, owner);
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
     * fingerprint of a table, which is what carries the declared values of an enum.
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
            var refTable = col.IsRef ? col.RefTable?.Storage : null;
            // one descriptor, four facets - this row is exactly where they must agree
            var ti = col.ToSqlDbTypeInfo();
            return $"\t({Str(table.SqlSchema)}, {Str(table.Table)}, {Str(col.Name)}, {Str(ti.SqlName)}, " +
                $"{Num(ti.Length)}, {Num(ti.Precision)}, {Num(ti.Scale)}, " +
                $"{(col.DeployNullable() ? 1 : 0)}, {Str(refTable?.SqlSchema)}, {Str(refTable?.Table)}, " +
                $"{Str(col.DeployDefault())})";
        }

        void AddTable(TableMetadata t)
        {
            sqlTables.Add($"\t({Str(t.SqlSchema)}, {Str(t.Table)}, {Str(t.Xtra())})");
            foreach (var col in t.AllColumns())
                sqlColumns.Add(ColumnRow(t, col));
        }

        foreach (var (t, _) in DeployTables(tables))
            AddTable(t);

        // the hash is taken from the text, so the order must not depend on how the
        // file system happens to be enumerated
        sqlTables.Sort(StringComparer.Ordinal);
        sqlColumns.Sort(StringComparer.Ordinal);

        var rowDiv = $",{Environment.NewLine}\t";
        var sqlScript = $"""
        /* METADATA SEED. Version: {version} */
        begin
            set nocount on;
            declare @tables table([schema] sysname, [table] sysname, [xtra] nvarchar(64));
            declare @columns table([schema] sysname, [table] sysname, [column] sysname, [datatype] sysname,
                [length] int, [precision] tinyint, [scale] tinyint, [nullable] bit,
                [ref_schema] nvarchar(128), [ref_table] nvarchar(128), [default] nvarchar(128));

            insert into @tables([schema], [table], [xtra]) values
            {String.Join(rowDiv, sqlTables)};

            insert into @columns([schema], [table], [column], [datatype],
                [length], [precision], [scale], [nullable], [ref_schema], [ref_table], [default]) values
            {String.Join(rowDiv, sqlColumns)};

            -- merge tables
            merge a2meta.Tables as t
            using @tables as s
            on t.[schema] = s.[schema] and t.[table] = s.[table]
            when matched then update set
                t.[xtra] = s.[xtra]
            when not matched then insert([schema], [table], [xtra]) values
               (s.[schema], s.[table], s.[xtra])
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

    public async Task DeployDatabaseAsync(String? dataSource, String allScript)
    {
        if (String.IsNullOrWhiteSpace(allScript))
            return;
        var scripts = allScript.Split($"{Environment.NewLine}go");

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
                lineFrom = lineTo + 1; // + go
            }
        }
        catch (Exception ex)
        {
            throw new DeployScriptException(ex, lineFrom, lineTo);
        }
    }
}

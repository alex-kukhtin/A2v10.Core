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
        allScript.AppendLine(CreateForeignKeysScript(tables));
        //* 5. INDEXES

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

    private String CreateTablesScript(IEnumerable<TableMetadata> tables)
    {
        var strBuilder = new StringBuilder();
        strBuilder.AppendLine("-- TABLES");
        foreach (var table in tables)
        {
            strBuilder.AppendLine(_dbCreator.CreateTable(table));
            strBuilder.AppendLine("go");
            if (table.HasTags)
            {
                strBuilder.AppendLine(_dbCreator.CreateTable(TableMetadataDefaults.CreateTagEntriesTable(table)));
                strBuilder.AppendLine("go");
            }
            foreach (var d in table.Details)
            {
                strBuilder.AppendLine(_dbCreator.CreateTable(d.Value));
                strBuilder.AppendLine("go");
            }
        }

        if (tables.Any(t => t.HasTags)) 
        {
            strBuilder.AppendLine(_dbCreator.CreateTable(TableMetadataDefaults.TagsTable()));
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
        foreach (var table in tables)
        {
            var fc = CliDatabaseCreator.CreateForeignKeys(table);
            if (!String.IsNullOrWhiteSpace(fc))
            {
                strBuilder.AppendLine(fc);
                strBuilder.AppendLine("go");
            }
            // Owner -> the tagged table, Tag -> the tags catalog. Both addresses are known
            // right here, which is why the tag entries table is passed its owner like a detail.
            if (table.HasTags)
            {
                fc = CliDatabaseCreator.CreateForeignKeys(TableMetadataDefaults.CreateTagEntriesTable(table), table);
                if (!String.IsNullOrWhiteSpace(fc))
                {
                    strBuilder.AppendLine(fc);
                    strBuilder.AppendLine("go");
                }
            }
            foreach (var d in table.Details)
            {
                fc = CliDatabaseCreator.CreateForeignKeys(d.Value, table);
                if (!String.IsNullOrEmpty(fc))
                {
                    strBuilder.AppendLine(fc);
                    strBuilder.AppendLine("go");
                }
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

        foreach (var t in tables)
        {
            AddTable(t);
            if (t.HasTags)
                AddTable(TableMetadataDefaults.CreateTagEntriesTable(t));
            foreach (var d in t.Details.Values)
                AddTable(d);
        }

        if (tables.Any(t => t.HasTags))
            AddTable(TableMetadataDefaults.TagsTable());

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

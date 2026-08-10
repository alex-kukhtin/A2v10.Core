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

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Metadata.Cli;
using Microsoft.Data.SqlClient;

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

    public async Task<DeployDatabaseResult> CheckDeployAsync(String? dataSource, IEnumerable<TableMetadata> tables)
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
        allScript.AppendLine(CreateTablesScript(tables));
        allScript.AppendLine(CreateTableTypesScript(tables));
        allScript.AppendLine(SyncSchemaScript());
        allScript.AppendLine(CreateForeignKeysScript(tables));
        //* 5.Deploy INDEXES

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
        return strBuilder.ToString();
    }

    private static String CreateTableTypesScript(IEnumerable<TableMetadata> tables)
    {
        static Boolean HasTableType(TableMetadata table)
            => !table.IsJournal && !table.IsTags && !table.IsTagEntries;

        var strBuilder = new StringBuilder();
        strBuilder.AppendLine("-- TABLE TYPES");
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
     */

    private static Version assVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new();

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

        foreach (var t in tables)
        {
            sqlTables.Add($"\t({Str(t.SqlSchema)}, {Str(t.Table)})");
            foreach (var col in t.AllColumns())
                sqlColumns.Add(ColumnRow(t, col));
            foreach (var d in t.Details.Values)
            {
                sqlTables.Add($"\t({Str(d.SqlSchema)}, {Str(d.Table)})");
                foreach (var col in d.AllColumns())
                    sqlColumns.Add(ColumnRow(d, col));
            }
        }

        // the hash is taken from the text, so the order must not depend on how the
        // file system happens to be enumerated
        sqlTables.Sort(StringComparer.Ordinal);
        sqlColumns.Sort(StringComparer.Ordinal);

        var rowDiv = $",{Environment.NewLine}\t";
        var sqlScript = $"""
        /* METADATA SEED. Version: {version} */
        begin
            set nocount on;
            declare @tables table([schema] sysname, [table] sysname);
            declare @columns table([schema] sysname, [table] sysname, [column] sysname, [datatype] sysname,
                [length] int, [precision] tinyint, [scale] tinyint, [nullable] bit,
                [ref_schema] nvarchar(128), [ref_table] nvarchar(128), [default] nvarchar(128));

            insert into @tables([schema], [table]) values
            {String.Join(rowDiv, sqlTables)};

            insert into @columns([schema], [table], [column], [datatype],
                [length], [precision], [scale], [nullable], [ref_schema], [ref_table], [default]) values
            {String.Join(rowDiv, sqlColumns)};

            -- merge tables
            merge a2meta.Tables as t
            using @tables as s
            on t.[schema] = s.[schema] and t.[table] = s.[table]
            when not matched then insert([schema], [table]) values
               (s.[schema], s.[table])
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

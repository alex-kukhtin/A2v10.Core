// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Metadata.Cli;

namespace A2v10.Metadata;

internal sealed record DbHash
{
    public String? Hash { get; set; }
}

public class SqlDbGenerator(IAppCodeProvider _appCodeProvider, IDbContext  _dbContext)
{
    private const String DB_FILE = "deploydatabase.sql";
    
    private readonly CliDatabaseCreator _dbCreator = new();

    private String DatabaseFilePath => _appCodeProvider.GetMainModuleFullPath("_sqlscripts", DB_FILE);

    public async Task<Boolean> CheckDeployAsync(String? dataSource, IEnumerable<TableMetadata> tables)
    {
        var seedScript = await GenerateMetadataSeedAsync(tables);
        var seedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seedScript ?? "new"))).ToLowerInvariant();
        // read hash from DB
        var dbHash = await _dbContext.LoadAsync<DbHash>(dataSource, "a2meta.[GetDbHash]");

        if (dbHash?.Hash == seedHash)
            return true;

        var allScript = new StringBuilder();

        // CREATE SCRIPT
        allScript.AppendLine(seedScript);
        allScript.AppendLine(CreateTablesScript(tables));
        allScript.AppendLine(CreateTableTypesScript(tables));
        allScript.AppendLine(SyncSchemaScript());
        allScript.AppendLine(CreateForeignKeysScript(tables));
        //* 5.Deploy INDEXES

       await WriteDeployDatabaseFileAsync(allScript.ToString());

        // DEPLOY DATABASE
        await DeployDatabaseAsync(dataSource, allScript.ToString());

        // save hash
        await _dbContext.ExecuteAsync<DbHash>(dataSource, "a2meta.[SetDbHash]", new DbHash() { Hash = seedHash });
        return true;
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
            => !table.IsJournal;

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

    private static async Task<String?> GenerateMetadataSeedAsync(IEnumerable<TableMetadata> tables)
    {
        if (!tables.Any())
            return null;
        var sqlTables = new List<String>();
        var sqlColumns = new List<String>();

        foreach (var t in tables)
        {
            var tSchema = t.Schema.ToSqlSchema();
            sqlTables.Add($"\t('{tSchema}', '{t.Table}')");
            foreach (var col in t.AllColumns())
                sqlColumns.Add($"\t('{tSchema}', '{t.Table}', '{col.Name}', '{col.Type.ToSqlDataTypeDeploy()}')");
            foreach (var d in t.Details.Values)
            {
                var dSchema = d.Schema.ToSqlSchema();   
                sqlTables.Add($"\t('{dSchema}', '{d.Table}')");
                foreach (var col in d.Columns)
                    sqlColumns.Add($"\t('{dSchema}', '{d.Table}', '{col.Name}', '{col.Type.ToSqlDataTypeDeploy()}')");
            }
        }

        var rowDiv = $",{Environment.NewLine}\t";
        var sqlScript = $"""
        /* METADATA SEED */
        begin
            set nocount on;
            declare @tables table([schema] sysname, [table] sysname);
            declare @columns table([schema] sysname, [table] sysname, [column] sysname, [datatype] sysname);
            
            insert into @tables([schema], [table]) values
            {String.Join(rowDiv, sqlTables)};

            insert into @columns([schema], [table], [column], [datatype]) values
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
                t.[datatype] = s.[datatype]
            when not matched then insert
                ([schema], [table], [column], [datatype]) values
                (s.[schema], s.[table], s.[column], s.[datatype])
            when not matched by source then delete;
        end
        go
        """;

        return sqlScript;
    }

    public async Task DeployDatabaseAsync(String? dataSource, String allScript)
    {
        var scripts = allScript.Split($"{Environment.NewLine}go");
        if (scripts.Length == 0)
            return;

        using var dbConn = await _dbContext.GetDbConnectionAsync(dataSource);
        using var cmd = dbConn.CreateCommand();
        foreach (var line in scripts)
        {
            cmd.CommandText = line;
            cmd.ExecuteNonQuery();
        }
    }
}

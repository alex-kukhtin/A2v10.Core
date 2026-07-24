// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

public class SqlDbGenerator(IAppCodeProvider _appCodeProvider)
{
    public async Task<String?> GenerateMetadataSeedAsync(IEnumerable<TableMetadata> tables)
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
        begin
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
            when not matched then delete;

            -- merge columns
            merge a2meta.Columns as t
            using @columns as s
            on t.[schema] = s.[schema] and t.[table] = s.[table] and t.[column] = s.[column]
            when matched then update
                t.[datatype] = s.[datatype]
            when not matched then insert
                ([schema], [table], [column], [datatype]) values
                (s.[schema], s.[table], s.[column], s.[datatype])
            when not matched then delete;
        end
        go
        """;

        var seedPath = _appCodeProvider.GetMainModuleFullPath("_sql", "_schema_seed.sql");
        var seedDir = Path.GetDirectoryName(seedPath);
        if (String.IsNullOrWhiteSpace(seedDir))
            throw new InvalidOperationException($"Invalid path, {seedPath}");
        Directory.CreateDirectory(seedDir);
        await File.WriteAllTextAsync(seedPath, sqlScript, Encoding.UTF8);

       return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sqlScript))).ToLowerInvariant();
    }

    public Task DeployDatabaseAsync()
    {
        /*
         * 1. Deploy CREATE_TABLES
         * 2. Deploy TABLE_TYPES
         * 3. exec a2meta.[SyncSchema]
         * 4. Deploy FOREIGN_KEYS
         * 5. Deploy INDEXES
         */ 
        throw new NotFiniteNumberException("DEPLOY DATABASE HERE");
    }
}

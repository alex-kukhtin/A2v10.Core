// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata.Cli;

public class CliDeployDatabase(DatabaseMetadataProvider _metadataProvider, IAppCodeProvider _codeProvider,
    IDbContext _dbContext, CliDatabaseCreator _dbCreator)
{
    public async Task CreateTableType(String schema, String table)
    {
        var tableMeta = await _metadataProvider.GetSchemaAsync(null, schema, table);
        await DeployTableType(tableMeta);
    }

    public Task DeployTableType(TableMetadata table)
    {
        var sqlString = _dbCreator.CreateTableType(table);
        return _dbContext.LoadModelSqlAsync(null, sqlString);
    }

    public async Task DeployEndpoint(String schema, String table)
    {
        var tableMeta = await _metadataProvider.GetSchemaAsync(null, schema, table);

        await DeployTableType(tableMeta);

        // update table hash
        ExpandoObject prms = new()
        {
            {"Schema", schema },
            {"Table", table },
            {"Hash", tableMeta.FileHash }
        };
        await _dbContext.ExecuteExpandoAsync(null, "a2meta.[Endpoint.Hash]", prms);
    }


    public async Task DeployDatabaseAsync(Boolean verbose, Action<String> writeMsg)
    {
        void writeVerboseMsg(String msg)
        {
            if (!verbose)
                return;
            writeMsg(msg);
        }

        var ep = (await CollectEndpointsAsync()).ToList();
        var tables = ep.GroupBy(t => t.SqlTableName).Select(g => g.First()).ToList();

        //TODO: Тут надо загрузить хеши всех таблиц
        writeMsg("Deploying...");
        
        writeVerboseMsg("  Tables:");
        foreach (var table in tables)
        {
            writeVerboseMsg($"    {table.SqlTableName}");
            // create or alter
        }

        writeVerboseMsg("  Foreign keys:");
        foreach (var table in tables)
        {
            writeVerboseMsg($"    {table.SqlTableTypeName}");
            // foreign keys
        }


        writeVerboseMsg("  Table types:");
        foreach (var table in tables)
        {
            writeVerboseMsg($"    {table.SqlTableTypeName}");
            // table types
            await DeployTableType(table);
        }
        writeMsg($"Done. {tables.Count} updated, {ep.Count - tables.Count} skipped");

        // save new hashes
    }

    async Task<IEnumerable<TableMetadata>> CollectEndpointsAsync()
    {
        var src = _codeProvider.GetMainModuleFullPath(".", String.Empty);

        String[] schemas = ["catalog", "document", "enum"];
        String[] flatSchemas = ["document"];

        IEnumerable<(String schema, String table)> allEndpoints = schemas
            .Select(schema => Path.Combine(src, schema))
            .Where(Directory.Exists)
            .SelectMany(schemaDir => {
                var schema = Path.GetFileName(schemaDir);
                var tables = Directory.EnumerateDirectories(schemaDir)
                    .Select(tableDir => (schema, Path.GetFileName(tableDir)));
                    return tables;
                }
             );

        var result = new List<TableMetadata>();
        foreach (var ep in allEndpoints)
        {
            var meta = await _metadataProvider.GetSchemaAsync(null, ep.schema, ep.table);
            result.Add(meta);
        }
        return result;
    }
}

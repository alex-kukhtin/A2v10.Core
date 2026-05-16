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

    public async Task<IEnumerable<TableMetadata>> CollectEndpoints(Boolean forSql)
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
        return forSql ? result.GroupBy(t => t.SqlTableName).Select(g => g.First())
            : result;
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata.SqlServer;

public class DatabaseMetadataProvider(DatabaseMetadataCache _metadataCache, IDbContext _dbContext)
{
    public Task<TableMetadata> GetSchemaAsync(IModelBaseMeta meta, String? dataSource)
    {
        return _metadataCache.GetOrAddAsync(dataSource, meta.CurrentSchema, meta.CurrentTable, LoadTableMetadataAsync);
    }
    public Task<TableMetadata> GetSchemaAsync(String? dataSource, String schema, String table)
    {
        return _metadataCache.GetOrAddAsync(dataSource, schema, table, LoadTableMetadataAsync);
    }
    public String GetOrAddEndpointPath(String? dataSource, String schema, String table)
    {
        return _metadataCache.GetOrAddEndpointPath(dataSource, schema, table);
    }

    public async Task<EndpointTableInfo> GetModelInfoFromPathAsync(String path)
    {
        var modelTableInfo = _metadataCache.GetModelInfoFromPath(path);
        if (modelTableInfo == null) {
            var pathInfo = ParsePath(path);
            var meta = await _metadataCache.GetOrAddAsync(null, pathInfo.schema, pathInfo.table,
                LoadTableMetadataAsync);
            // case sensitive
            pathInfo.table = meta.Table;
            pathInfo.schema = meta.Schema;
            _metadataCache.GetOrAddEndpointPath(null, pathInfo.schema, pathInfo.table);
            modelTableInfo = _metadataCache.GetModelInfoFromPath(path);
        }
        if (modelTableInfo == null)
            throw new InvalidOperationException("GetModelInfo fails");
        return modelTableInfo;
    }

    private async Task<TableMetadata> LoadTableMetadataAsync(String? dataSource, String schema, String table)
    {
        var prms = new ExpandoObject()
        {
            {"Schema", schema},
            {"Table", table},
        };
        var dm = await _dbContext.LoadModelAsync(dataSource, "a2meta.[Table.Schema]", prms)
            ?? throw new InvalidOperationException("a2meta.[Table.Schema] returns null");
        var json = JsonConvert.SerializeObject(dm.Eval<ExpandoObject>("Table"));
        if (json == null)
            throw new InvalidOperationException("TableMetadata not found");
        var meta = JsonConvert.DeserializeObject<TableMetadata>(json)
            ?? throw new InvalidOperationException("TableMetadata deserialization fails");
        meta.OnEndInit();
        return meta;
    }

    private (String schema, String table) ParsePath(String path)
    {
        var split = path.Split('/');
        if (split.Length < 2 )
            throw new InvalidOperationException($"Invalid path: {path}");
        var schema = split[0] switch
        {
            "catalog" => "cat",
            "document" => "doc",
            _ => "unknown schema"
        };
        return (schema, split[1].Plural());
    }
}

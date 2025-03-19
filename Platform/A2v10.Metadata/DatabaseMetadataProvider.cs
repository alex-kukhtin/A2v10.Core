// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

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

    public Task<AppMetadata> GetAppMetadataAsync(String? dataSource)
    {
        return _metadataCache.GetAppMetadataAsync(dataSource, LoadAppMetadataAsync);
    }

    public String GetOrAddEndpointPath(String? dataSource, String schema, String table)
    {
        return _metadataCache.GetOrAddEndpointPath(dataSource, schema, table);
    }

    public Task<Form?> GetFormAsync(String? dataSource, String schema, String table, String key)
    {
        return _metadataCache.GetOrAddFormAsync(dataSource, schema, table, key, LoadTableFormAsync);
    }

    public async Task<EndpointTableInfo> GetModelInfoFromPathAsync(String path)
    {
        var modelTableInfo = _metadataCache.GetModelInfoFromPath(path);
        if (modelTableInfo == null) {
            var pathInfo = ParsePath(path);
            var tableMeta = await _metadataCache.GetOrAddAsync(null, pathInfo.schema, pathInfo.table,
                LoadTableMetadataAsync);
            // case sensitive
            pathInfo.table = tableMeta.Name;
            pathInfo.schema = tableMeta.Schema;
            _metadataCache.GetOrAddEndpointPath(null, pathInfo.schema, pathInfo.table);
            modelTableInfo = _metadataCache.GetModelInfoFromPath(path);
        }
        if (modelTableInfo == null)
            throw new InvalidOperationException("GetModelInfo fails");
        return modelTableInfo;
    }

    private async Task<AppMetadata> LoadAppMetadataAsync(String? dataSource)
    {
        var dm = await _dbContext.LoadModelAsync(dataSource, "a2meta.[App.Metadata]")
            ?? throw new InvalidOperationException("a2meta.[App.Metadata] returns null");
        var appExpando = dm.Eval<ExpandoObject>("Application");
        var json = JsonConvert.SerializeObject(appExpando) ??
            throw new InvalidOperationException("AppMetadata not found");
        var meta = JsonConvert.DeserializeObject<AppMetadata>(json, JsonSettings.IgnoreNull)
            ?? throw new InvalidOperationException("AppMetadata deserialization fails");
        return meta;
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
        var tableExpando = dm.Eval<ExpandoObject>("Table");
        if (tableExpando == null && schema == "a2meta")
            return new TableMetadata();
        var json = JsonConvert.SerializeObject(tableExpando);
        if (json == null)
            throw new InvalidOperationException("TableMetadata not found");
        var meta = JsonConvert.DeserializeObject<TableMetadata>(json, JsonSettings.IgnoreNull)
            ?? throw new InvalidOperationException("TableMetadata deserialization fails");
        return meta;
    }

    private async Task<Form?> LoadTableFormAsync(String? dataSource, String schema, String table, String key)
    {
        var prms = new ExpandoObject()
        {
            {"Schema", schema},
            {"Table", table},
            {"Key", key},
        };
        var dm = await _dbContext.LoadModelAsync(dataSource, "a2meta.[Table.Form]", prms);
        if (dm == null)
            return null;
        var formExpando = dm.Eval<ExpandoObject>("Form");
        if (formExpando == null)
            return null;
        var json = JsonConvert.SerializeObject(formExpando);
        if (json == null)
            throw new InvalidOperationException("Form not found");
        var meta = JsonConvert.DeserializeObject<Form>(json, JsonSettings.IgnoreNull)
            ?? throw new InvalidOperationException("Form deserialization fails");
        return meta;
    }

    private (String schema, String table) ParsePath(String path)
    {
        var split = path.Split('/');
        if (split.Length < 2 )
            throw new InvalidOperationException($"Invalid path: {path}");
        var schema = split[0] switch
        {
            "catalogs" => "cat",
            "documents" => "doc",
            _ => "unknown schema"
        };
        return (schema, split[1]);
    }
}

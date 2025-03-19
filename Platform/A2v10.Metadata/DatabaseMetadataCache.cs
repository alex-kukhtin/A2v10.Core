// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

// SINGLETON

public class DatabaseMetadataCache
{
    ConcurrentDictionary<String, TableMetadata> _cache = [];
    ConcurrentDictionary<String, EndpointTableInfo> _endpoints = [];
    ConcurrentDictionary<String, Form?> _formCache = [];
    public async Task<TableMetadata> GetOrAddAsync(String? dataSource, String schema, String table, 
        Func<String?, String, String, Task<TableMetadata>> getMeta)
    {
        var key = $"{dataSource}:{schema}:{table}";
        if (_cache.TryGetValue(key, out TableMetadata? meta))
            return meta;
        meta = await getMeta(dataSource, schema, table);
        key = $"{dataSource}:{meta.Schema}:{meta.Name}";
        var globalMeta = await GetGlobalMetaAsync(dataSource, getMeta);
        meta = meta.MergeGlobal(globalMeta);
        return _cache.GetOrAdd(key, meta);
    }

    async Task<TableMetadata> GetGlobalMetaAsync(String? dataSource,
        Func<String?, String, String, Task<TableMetadata>> getMeta)
    {
        var schema = "a2meta";
        var table = "TablesMetadata";
        var key = $"{dataSource}:{schema}:{table}";
        if (_cache.TryGetValue(key, out TableMetadata? meta))
            return meta;
        meta = await getMeta(dataSource, schema, table);
        return _cache.GetOrAdd(key, meta);
    }

    public async Task<Form?> GetOrAddFormAsync(String? dataSource, String schema, String table, String key,
        Func<String?, String, String, String, Task<Form?>> getForm)
    {
        var dictKey = $"{dataSource}:{schema}:{table}:{key.ToLowerInvariant()}";
        if (_formCache.TryGetValue(dictKey, out Form? form))
            return form;
        form = await getForm(dataSource, schema, table, key);
        return _formCache.GetOrAdd(dictKey, form);
    }

    public String GetOrAddEndpointPath(String? dataSource, String schema, String table)
    {
        var segment0 = schema switch
        {
            "cat" => "catalog",
            "doc" => "document",
            _ => schema
        };
        var path = $"{segment0}/{table.Singular()}".ToLowerInvariant();
        _endpoints.TryAdd(path, new EndpointTableInfo(dataSource, schema, table));
        return path;
    }

    public EndpointTableInfo? GetModelInfoFromPath(String path)
    {
        if (_endpoints.TryGetValue(path, out var modelInfo))
            return modelInfo;
        return null;
    }
}

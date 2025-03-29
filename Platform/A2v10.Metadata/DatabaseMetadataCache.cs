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
    ConcurrentDictionary<String, FormMetadata> _formCache = [];
    ConcurrentDictionary<String, AppMetadata> _appMetaCache = [];
    public async Task<TableMetadata> GetOrAddAsync(String? dataSource, String schema, String table, 
        Func<String?, String, String, Task<TableMetadata>> getMeta)
    {
        var key = $"{dataSource}:{schema}:{table}";
        if (_cache.TryGetValue(key, out TableMetadata? meta))
            return meta;
        meta = await getMeta(dataSource, schema, table);
        key = $"{dataSource}:{meta.Schema}:{meta.Name}";
        //var globalMeta = await GetGlobalMetaAsync(dataSource, getMeta);
        //meta = meta.MergeGlobal(globalMeta);
        return _cache.GetOrAdd(key, meta);
    }

    public async Task<AppMetadata> GetAppMetadataAsync(String? dataSource, Func<String?, Task<AppMetadata>> func)
    {
        var key = dataSource ?? "default";
        if (_appMetaCache.TryGetValue(key, out AppMetadata? meta))
            return meta;    
        meta = await func(dataSource);
        return _appMetaCache.GetOrAdd(key, meta);
    }


    public async Task<FormMetadata> GetOrAddFormAsync(String? dataSource, TableMetadata meta, String key,
    Func<String?, TableMetadata, String, Func<Form>, Task<FormMetadata>> getForm, Func<Form> getDefaultForm)
    {
        var dictKey = $"{dataSource}:{meta.Schema}:{meta.Name}:{key.ToLowerInvariant()}";
        if (_formCache.TryGetValue(dictKey, out var form))
            return form;
        form = await getForm(dataSource, meta, key, getDefaultForm);
        return form; // TODO: CACHE
        return _formCache.GetOrAdd(dictKey, form);
    }

    public String GetOrAddEndpointPath(String? dataSource, String schema, String table)
    {
        var path = $"{schema.ToFolder()}/{table}".ToLowerInvariant();
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

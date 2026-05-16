// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata;

// SINGLETON

public class DatabaseMetadataCache
{
    private readonly ConcurrentDictionary<String, TableMetadata> _cache = [];
    private readonly ConcurrentDictionary<String, EndpointTableInfo> _endpoints = [];
    private readonly ConcurrentDictionary<String, UIElement> _xamlFormCache = [];
    private readonly ConcurrentDictionary<String, AppMetadata> _appMetaCache = [];

    private FileSystemWatcher? FileWatcher { get; init; }
    public DatabaseMetadataCache(IAppCodeProvider appCodeProvider, IOptions<AppOptions> appOptions)
    {
        if (appOptions.Value.Environment.Watch)
            FileWatcher = CreateWatcher(appCodeProvider);

    }
    public void ClearAll()
    {
        _cache.Clear();
        _endpoints.Clear();
        _appMetaCache.Clear();
        _xamlFormCache.Clear();
    }

    public async Task<TableMetadata> GetOrAddAsync(String? dataSource, String schema, String table, 
        Func<String?, String, String, Task<TableMetadata>> getMeta)
    {
        var key = $"{dataSource}:{schema}:{table}";
        if (_cache.TryGetValue(key, out TableMetadata? meta))
            return meta;
        meta = await getMeta(dataSource, schema, table);
        //key = $"{dataSource}:{meta.Schema}:{meta.Name}";
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

    public async Task<UIElement> GetOrAddXamlFormAsync(String? dataSource, TableMetadata meta, String key,
         Func<UIElement> getDefaultForm)
    {
        var dictKey = $"{dataSource}:{meta.Schema}:{meta.Model}:{key.ToLowerInvariant()}";
        if (_xamlFormCache.TryGetValue(dictKey, out var form))
            return form;
        form = getDefaultForm();
        return _xamlFormCache.GetOrAdd(dictKey, form);
    }

    public String GetOrAddEndpointPath(String? dataSource, String path, String schema, String table)
    {
        _endpoints.TryAdd(path, new EndpointTableInfo(dataSource, schema, table));
        return path;
    }

    public EndpointTableInfo? GetModelInfoFromPath(String path)
    {
        if (_endpoints.TryGetValue(path, out var modelInfo))
            return modelInfo;
        return null;
    }

    private void Watcher_Changed(Object sender, FileSystemEventArgs e)
    {
        ClearAll(); // All items! References!
    }
    private FileSystemWatcher? CreateWatcher(IAppCodeProvider appCodeProvider)
    {
        var path = appCodeProvider.GetMainModuleFullPath(".", String.Empty);
        if (String.IsNullOrEmpty(path))
            return null;
        var watcher = new FileSystemWatcher(path, "metadata.json")
        {
            IncludeSubdirectories = true,            
            NotifyFilter =
                NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
                | NotifyFilters.FileName | NotifyFilters.CreationTime
        };
        watcher.Changed += Watcher_Changed;
        watcher.Created += Watcher_Changed;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }
}

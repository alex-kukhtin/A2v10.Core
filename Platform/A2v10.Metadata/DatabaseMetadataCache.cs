// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata;

// SINGLETON

public class DatabaseMetadataCache
{
    private readonly ConcurrentDictionary<String, EndpointMetadata> _cache = [];
    /* Our tables, keyed by the file that declares them - not by the endpoint that asks.
     * Several endpoints share one entry, and this is also the deploy set.
     */
    private readonly ConcurrentDictionary<String, TableMetadata> _storages = [];
    private readonly ConcurrentDictionary<String, EndpointTableInfo> _endpoints = [];
    private readonly ConcurrentDictionary<String, UIElement> _xamlFormCache = [];
    private readonly ConcurrentDictionary<String, AppMetadata> _appMetaCache = [];
    // Keyed by data source because that is exactly what it describes: one data source is
    // one database, and the platformid base belongs to the database.
    private readonly ConcurrentDictionary<String, AppPlatformId> _platformIdCache = [];

    /* Held for the whole of one cold load - see GetOrLoadAsync - and by ClearAll, so that a
     * file change cannot land in the middle of a load and let it publish a table of the
     * generation that was just dropped.
     */
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private Boolean _metadataDirty = true; // TODO. ????
    private FileSystemWatcher? FileWatcher { get; init; }
    public Boolean IsMetadataDirty => _metadataDirty;
    public void ClearDirty() => _metadataDirty = false;
    public DatabaseMetadataCache(IAppCodeProvider appCodeProvider, IOptions<AppOptions> appOptions)
    {
        if (appOptions.Value.Environment.Watch)
            FileWatcher = CreateWatcher(appCodeProvider);

    }
    public void ClearAll()
    {
        _loadGate.Wait();    // never between the first build of a load and its publication
        try
        {
            _cache.Clear();
            _storages.Clear();   // both, always: a container must never keep a table of an older generation
            _endpoints.Clear();
            _appMetaCache.Clear();
            _platformIdCache.Clear();
            _xamlFormCache.Clear();
            _metadataDirty = true;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    /* An endpoint is not finished when it is built: the reference graph it points into is built
     * afterwards, and that graph is cyclic - two catalogs may name each other. So two things
     * have to hold at once, and they pull in opposite directions:
     *
     *   a descent that comes back around must find the instance and return, or it never ends;
     *   nobody else may receive an endpoint whose references are still being filled.
     *
     * What separates the two is not the endpoint's state but who is asking, so that is what is
     * distinguished here. The load in progress is an object, and only the descent holding it
     * can see into it. Everyone else sees _cache, which holds finished endpoints only and
     * receives a whole load at once.
     *
     * The gate is therefore not about the dictionary - that one is concurrent already - but
     * about the interval between the first build and the last link, which has no other way to
     * be made indivisible. It is taken on a miss only, so a warm cache is lock-free.
     *
     * A load that throws publishes nothing: the next request builds again and throws again,
     * which is the behaviour a broken metadata.json should have.
     */
    internal async Task<EndpointMetadata> GetOrLoadAsync(String? dataSource, String schema, String table,
        Func<EndpointLoad, String?, String, String, Task<EndpointMetadata>> load)
    {
        var key = EndpointLoad.KeyOf(dataSource, schema, table);
        if (_cache.TryGetValue(key, out EndpointMetadata? finished))
            return finished;
        await _loadGate.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out finished))
                return finished;
            var inLoad = new EndpointLoad(_cache);
            var endpoint = await load(inLoad, dataSource, schema, table);
            inLoad.Publish();
            return endpoint;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public async Task<TableMetadata> GetOrAddStorageAsync(String? dataSource, String schema, String table,
        Func<String?, String, String, Task<TableMetadata>> getStorage)
    {
        var key = $"{dataSource}:{schema}:{table}";
        if (_storages.TryGetValue(key, out TableMetadata? storage))
            return storage;
        storage = await getStorage(dataSource, schema, table);
        return _storages.GetOrAdd(key, storage);
    }

    public IEnumerable<TableMetadata> Storages => _storages.Values;

    public async Task<AppMetadata> GetAppMetadataAsync(String? dataSource, Func<String?, Task<AppMetadata>> func)
    {
        var key = dataSource ?? "default";
        if (_appMetaCache.TryGetValue(key, out AppMetadata? meta))
            return meta;    
        meta = await func(dataSource);
        return _appMetaCache.GetOrAdd(key, meta);
    }

    internal async Task<AppPlatformId> GetPlatformIdAsync(String? dataSource, Func<String?, Task<AppPlatformId>> func)
    {
        var key = dataSource ?? "default";
        if (_platformIdCache.TryGetValue(key, out AppPlatformId? platformId))
            return platformId;
        platformId = await func(dataSource);
        return _platformIdCache.GetOrAdd(key, platformId);
    }

    public async Task<UIElement> GetOrAddXamlFormAsync(String? dataSource, EndpointMetadata endpoint, String key,
         Func<UIElement> getDefaultForm)
    {
        // keyed by the endpoint, not by the table: endpoints sharing a table do not share forms
        var dictKey = $"{dataSource}:{endpoint.Path}:{key.ToLowerInvariant()}";
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

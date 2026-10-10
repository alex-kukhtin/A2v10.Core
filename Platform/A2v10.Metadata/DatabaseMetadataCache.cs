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
    private readonly ConcurrentDictionary<String, UIElement> _xamlFormCache = [];
    private readonly ConcurrentDictionary<String, IEnumerable<TableReferrer>> _referrers = [];
    // Keyed by data source because that is exactly what it describes: one data source is
    // one database, and the platformid base belongs to the database.
    private readonly ConcurrentDictionary<String, AppPlatformId> _platformIdCache = [];
    // app.json, read and checked once: one per application, not per data source - the file is the application's
    private AppJson? _appJson;
    // mcp.json and the roles of its paths; dropped with everything else, and alone by an edit of mcp.json or model.json
    private McpIndex? _mcpIndex;

    /* Held for the whole of one cold load - see GetOrLoadAsync - and by ClearAll, so that a
     * file change cannot land in the middle of a load and let it publish a table of the
     * generation that was just dropped.
     */
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    /* The metadata may differ from the database: true at start and after every file change, cleared
     * by a deploy of the generation that was dirty. The generation counts file changes, so a change
     * arriving DURING a deploy is not cleared by it - it belongs to the next.
     */
    private volatile Boolean _metadataDirty = true;
    private Int32 _generation;
    private readonly SemaphoreSlim _deployGate = new(1, 1);
    private FileSystemWatcher? FileWatcher { get; init; }

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
            /* Not metadata - this one comes from the database (a2meta.[GetFkReferrers]) - and the
             * only thing that moves it is a deploy. It belongs here anyway: the deploy is what the
             * next request runs because of the _metadataDirty set at the end of this block, so it
             * always follows this reset. Dropping it after the deploy instead would put the
             * invalidation in the one place that has no other reason to know this cache exists.
             *
             * What it costs to keep is not a stale message: DbRemove deletes by 'Void = 1', so no
             * row ever leaves and the foreign key the deploy has just created never fires. A set
             * that is one referrer short is a permitted delete of something already referenced.
             */
            _referrers.Clear();
            _platformIdCache.Clear();
            _appJson = null;
            _mcpIndex = null;
            _xamlFormCache.Clear();
            Interlocked.Increment(ref _generation);
            _metadataDirty = true;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    /* One deploy at a time - two first requests both saw the flag and ran the DDL on two connections -
     * and the flag is cleared for the generation that was deployed only.
     */
    internal async Task CheckDeployAsync(Func<Task> deploy)
    {
        if (!_metadataDirty)
            return;
        await _deployGate.WaitAsync();
        try
        {
            if (!_metadataDirty)
                return;
            var generation = _generation;
            await deploy();
            if (generation == _generation)
                _metadataDirty = false;
        }
        finally
        {
            _deployGate.Release();
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
            var inLoad = new EndpointLoad(_cache, _storages);
            var endpoint = await load(inLoad, dataSource, schema, table);
            inLoad.Publish();
            return endpoint;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    internal async Task<IEnumerable<TableReferrer>> GetTableReferrersAsync(String? dataSource, TableMetadata table,
        Func<String?, TableMetadata, Task<IEnumerable<TableReferrer>>> loader)
    {
        var key = EndpointLoad.KeyOf(dataSource, table.Schema, table.Table);
        if (_referrers.TryGetValue(key, out IEnumerable<TableReferrer>? referrers))
            return referrers;
        var res = await loader(dataSource, table);
        return _referrers.GetOrAdd(key, res);
    }

    internal async Task<AppPlatformId> GetPlatformIdAsync(String? dataSource, Func<String?, Task<AppPlatformId>> func)
    {
        var key = dataSource ?? "default";
        if (_platformIdCache.TryGetValue(key, out AppPlatformId? platformId))
            return platformId;
        platformId = await func(dataSource);
        return _platformIdCache.GetOrAdd(key, platformId);
    }

    // a race reads the file twice and keeps either: both are the same answer
    internal async Task<AppJson> GetAppJsonAsync(Func<Task<AppJson>> load) =>
        _appJson ??= await load();

    // the same race, the same answer
    internal async Task<McpIndex> GetMcpIndexAsync(Func<Task<McpIndex>> load) =>
        _mcpIndex ??= await load();

    public async Task<UIElement> GetOrAddXamlFormAsync(String? dataSource, EndpointMetadata endpoint, String key,
         Func<UIElement> getDefaultForm)
    {
        // keyed by the endpoint, not by the table: endpoints sharing a table do not share forms
        var dictKey = $"{dataSource}:{endpoint.Path}:{key.ToLowerInvariant()}";
        if (_xamlFormCache.TryGetValue(dictKey, out var form))
            return form;
        form = getDefaultForm();
        /* Initialized before it is shared: every request renders this one object, and a form other
         * threads could see half-initialized would be initialized by all of them at once. Two first
         * requests build and initialize two forms, and the cache keeps one.
         */
        form.InitComplete();
        return _xamlFormCache.GetOrAdd(dictKey, form);
    }

    private void Watcher_Changed(Object sender, FileSystemEventArgs e)
    {
        /* mcp.json and model.json feed the MCP index only. Everything else watched drops everything:
         * app.json holds what every load and the deploy read - aliases, platformid, roles; an
         * operation file and a seed file are baked into the endpoint beside them.
         */
        var name = Path.GetFileName(e.FullPath);
        if (name.Equals("mcp.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("model.json", StringComparison.OrdinalIgnoreCase))
            _mcpIndex = null;
        else
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
        watcher.Filters.Add("app.json");
        watcher.Filters.Add("mcp.json");
        watcher.Filters.Add("model.json");
        watcher.Filters.Add($"*{DatabaseMetadataProvider.OperationFileSuffix}");
        // by the convention the skill writes ('seed': 'seed.json'); a seed under another name is read, not watched
        watcher.Filters.Add("seed.json");
        watcher.Changed += Watcher_Changed;
        watcher.Created += Watcher_Changed;
        /* An editor that saves atomically writes a temp file and renames it over the original: the
         * only events naming metadata.json are then Deleted and Renamed. Deleted is also a real
         * change - an endpoint removed.
         */
        watcher.Renamed += Watcher_Changed;
        watcher.Deleted += Watcher_Changed;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace A2v10.Metadata;

/* One cold load: the endpoints built so far, none of which is visible to anyone outside it.
 *
 * It exists because a reference graph is cyclic and an endpoint is therefore reachable before
 * it is finished. Something has to be able to say 'this one is already being built, here it
 * is' - and that answer is only ever correct for the descent that is building it. Handing the
 * same answer to a concurrent request is how a half-linked endpoint escapes.
 *
 * So it is a parameter, threaded down the descent, and not a field or an ambient value. The
 * signature is the statement: a method that takes one is inside a load, a method that does not
 * is an entry point. Nothing has to be remembered, and nothing can be got wrong quietly.
 */
internal sealed class EndpointLoad(ConcurrentDictionary<String, EndpointMetadata> finished)
{
    private readonly Dictionary<String, EndpointMetadata> _loading = [];

    internal static String KeyOf(String? dataSource, String schema, String table) =>
        $"{dataSource}:{schema}:{table}";

    /* Both generations, this one first: what this load has built is newer than what the cache
     * holds, and during a load they cannot disagree anyway - ClearAll waits for the gate.
     */
    public EndpointMetadata? Find(String? dataSource, String schema, String table)
    {
        var key = KeyOf(dataSource, schema, table);
        if (_loading.TryGetValue(key, out var loading))
            return loading;
        return finished.TryGetValue(key, out var done) ? done : null;
    }

    /* Called before the endpoint's references are resolved, which is the entire point: the
     * descent that comes back around finds it here and stops instead of building it again.
     */
    public void Add(String? dataSource, String schema, String table, EndpointMetadata endpoint) =>
        _loading[KeyOf(dataSource, schema, table)] = endpoint;

    // Once, at the end, for all of them: a load is visible whole or not at all.
    public void Publish()
    {
        foreach (var (key, endpoint) in _loading)
            finished.TryAdd(key, endpoint);
    }
}

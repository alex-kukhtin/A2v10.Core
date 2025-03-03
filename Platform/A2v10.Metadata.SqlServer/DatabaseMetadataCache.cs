// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace A2v10.Metadata.SqlServer;

// SINGLETON
public class DatabaseMetadataCache
{
    ConcurrentDictionary<String, TableMetadata> _cache = [];
    public async Task<TableMetadata> GetOrAddAsync(String? dataSource, String schema, String table, 
        Func<String?, String, String, Task<TableMetadata>> getMeta)
    {
        var key = $"{dataSource}:{schema}:{table}";
        if (_cache.TryGetValue(key, out TableMetadata? meta))
            return meta;
        meta = await getMeta(dataSource, schema, table);
        return _cache.GetOrAdd(key, meta);
    }
}

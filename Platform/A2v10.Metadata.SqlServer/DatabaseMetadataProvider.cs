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
    public Task<TableMetadata> GetSchemaAsync(IModelJsonMeta meta, String? dataSource)
    {
        return _metadataCache.GetOrAddAsync(dataSource, meta.Schema, meta.Table, LoadTableMetadataAsync);
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
        return JsonConvert.DeserializeObject<TableMetadata>(json)
            ?? throw new InvalidOperationException("TableMetadata deserialization fails");
    }
}

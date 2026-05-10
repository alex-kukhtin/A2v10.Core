// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Metadata;

public class ResetFormHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
{
    private readonly DatabaseMetadataCache _metadataCache = _serviceProvider.GetRequiredService<DatabaseMetadataCache>();
    private readonly IDbContext _dbContext = _serviceProvider.GetRequiredService<IDbContext>();

    public async Task<Object> InvokeAsync(ExpandoObject args)
    {
        var key = args.Get<String>("Key")
            ?? throw new InvalidOperationException("Key is null");
        var prms = new ExpandoObject()
        {
            { "Id", args.Get<String>("Id") },
            { "Key", key },
        };

        var dm = await _dbContext.LoadModelAsync(null, "a2meta.[Table.Form.Reset]", prms);

        var schema = dm.Eval<String>("Table.Schema")
            ?? throw new InvalidOperationException("Table.Schema is null");
        var table = dm.Eval<String>("Table.Name")
            ?? throw new InvalidOperationException("Table.Name is null");
        _metadataCache.RemoveFormFromCache(null, schema, table, key);

        return new ExpandoObject();
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

public class ClearCacheHandler(IServiceProvider _serviceProvider) : IClrInvokeTarget
{
    private readonly DatabaseMetadataCache _metadataCache = _serviceProvider.GetRequiredService<DatabaseMetadataCache>();

    public Task<Object> InvokeAsync(ExpandoObject args)
    {
        var schema = args.Get<String>("Schema");
        var table = args.Get<String>("Table");
        _metadataCache.Clear(schema, table);

        return Task.FromResult<Object>(new ExpandoObject());
    }
}

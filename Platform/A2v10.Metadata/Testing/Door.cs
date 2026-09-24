// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Services.Api;

namespace A2v10.Metadata;

/* The door the browser uses - EndpointDataService over IDataService - a scope per call, as a
 * request has. Who the user is, the host decided (the harness registers it). Nothing here reaches past it into
 * the builders. A load renders the page as the browser's does and drops it: a screen that cannot
 * be built fails the step.
 */
internal sealed class Door(IServiceScopeFactory scopeFactory)
{
    public async Task<DataResult> OpenAsync(String at, String id)
    {
        using var scope = scopeFactory.CreateScope();
        return await At(scope, at).LoadAsync(id);
    }

    public async Task<DataResult> CreateAsync(String at)
    {
        using var scope = scopeFactory.CreateScope();
        return await At(scope, at).CreateAsync();
    }

    public async Task<DataResult> IndexAsync(String at)
    {
        using var scope = scopeFactory.CreateScope();
        return await At(scope, at).IndexAsync(new IndexQuery(), new ExpandoObject());
    }

    public async Task<ExpandoObject> SaveAsync(String at, ExpandoObject root)
    {
        using var scope = scopeFactory.CreateScope();
        return await At(scope, at).SaveAsync(root);
    }

    public async Task PostAsync(String at, Object id)
    {
        using var scope = scopeFactory.CreateScope();
        await At(scope, at).PostAsync(id);
    }

    public async Task UnPostAsync(String at, Object id)
    {
        using var scope = scopeFactory.CreateScope();
        await At(scope, at).UnPostAsync(id);
    }

    static DataAt At(IServiceScope scope, String at)
        => scope.ServiceProvider.GetRequiredService<EndpointDataService>().At(at.Trim('/'), render: true);
}

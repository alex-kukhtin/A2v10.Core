// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Services.Api;

namespace A2v10.Metadata;

/* The door the browser uses - EndpointDataService over IDataService - a scope per call, as a
 * request has. Who the user is, the host decided (the harness registers it). Nothing here reaches past it into
 * the builders. A load renders the page as the browser's does and drops it: a screen that cannot
 * be built fails the step. A report is the exception: its numbers are read through report_query's reading.
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

    // a report has no record to open: it is asked for numbers, the way report_query asks
    public async Task<(ReportEndpointMetadata Endpoint, ReportRows Rows)> ReportAsync(String at, JsonElement args)
    {
        using var scope = scopeFactory.CreateScope();
        var path = at.Trim('/').Split('/');
        if (path.Length != 2)
            throw new ScenarioException($"'{at}': a report is '/report/<folder>'");
        var metadata = scope.ServiceProvider.GetRequiredService<DatabaseMetadataProvider>();
        var endpoint = await metadata.GetReportEndpointAsync(null, path[0], path[1]);
        return (endpoint, await McpReport.RowsAsync(endpoint, at, args, scope.ServiceProvider, metadata));
    }

    static DataAt At(IServiceScope scope, String at)
        => scope.ServiceProvider.GetRequiredService<EndpointDataService>().At(at.Trim('/'), render: true);
}

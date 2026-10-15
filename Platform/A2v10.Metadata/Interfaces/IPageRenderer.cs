// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* What the page is drawn from, once the model is loaded and the template chosen; a new fact of the
 * page is a member here. Which page is the endpoint's TYPE - no string tells the renderer what it is.
 * The data source keys what the renderer keeps; null Template - the page runs none.
 */
internal sealed record PageRequest(EndpointMetadata Endpoint, IPlatformUrl PlatformUrl, String? DataSource,
    IModelView View, IDataModel Model, String? Template);

/* The page of a screen: the one question a builder asks the view engine. Optional - a host that
 * serves data only (an API, the backend of an SPA) registers none, and a builder refuses a render
 * there; a reload, which is all such a host asks for, never reaches it.
 */
internal interface IPageRenderer
{
    Task<String> RenderPageAsync(PageRequest request);

    // the generated view of a form as text - what 'a2 meta materialize' hands the author
    String ViewText(NormalEndpointMetadata endpoint, String action);

    // every page the endpoint draws without data, built and dropped - the view half of the screen stage of validate
    void BuildPages(EndpointMetadata endpoint);

    // the one refusal, worded once for every builder
    internal static IPageRenderer Of(IServiceProvider serviceProvider, String path) =>
        serviceProvider.GetService<IPageRenderer>()
            ?? throw new InvalidOperationException($"{path}: no page renderer is registered - this host serves data only");
}

// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* A report never goes through BaseModelBuilder: it has no table of its own to load, save or
 * render, and everything it needs is in its own container.
 */
internal class ReportEndpointBuilder(IServiceProvider _serviceProvider, ReportEndpointMetadata _endpoint,
    IPlatformUrl platformUrl, AppPlatformId _platformId, Boolean _useGrants) : IModelBuilder
{
    public String Path => _endpoint.Path;

    public async Task<IAppRuntimeResult> RenderAsync(IModelView view, Boolean isReload)
    {
        var reportBuilder = BaseReportBuilder.Create(_serviceProvider, _endpoint, _platformId, _useGrants);

        var dm = await reportBuilder.LoadReportModelAsync(view, platformUrl.Query ?? []);

        if (isReload)
            return new AppRuntimeResult(dm, null);

        var request = new PageRequest(_endpoint, platformUrl, view.DataSource, view, dm, reportBuilder.CreateTemplate());
        return new AppRuntimeResult(dm, await IPageRenderer.Of(_serviceProvider, Path).RenderPageAsync(request));
    }
}

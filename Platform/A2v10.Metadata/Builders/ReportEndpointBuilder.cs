// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Xaml.DynamicRendrer;
using A2v10.Xaml;


namespace A2v10.Metadata;

/* A report never goes through BaseModelBuilder: it has no table of its own to load, save or
 * render, and everything it needs is in its own container.
 */
internal class ReportEndpointBuilder(IServiceProvider _serviceProvider, ReportEndpointMetadata _endpoint,
    IPlatformUrl platformUrl, AppPlatformId _platformId) : IModelBuilder
{
    private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);

    public String Path => _endpoint.Path;

    public async Task<IAppRuntimeResult> RenderAsync(IModelView view, Boolean isReload)
    {
        var _source = _endpoint.Surface;
        var _report = _endpoint.Report;

        var reportBuilder = _report.Type switch
        {
            "turnover" => new TurnoverReportBuilder(_serviceProvider, _report, _source, _platformId),
            _ => throw new NotImplementedException($"Invalid Report type '{_report.Type}' for {_endpoint.Path}")
        };

        var dm = await reportBuilder.LoadReportModelAsync(view,  platformUrl.Query ?? []);

        if (isReload)
            return new AppRuntimeResult(dm, null);

        String rootId = $"el{Guid.NewGuid()}";
        String templateText = reportBuilder.CreateTemplate();

        UIElement page = reportBuilder.CreatePage();

        if (page is ISupportPlatformUrl supportPlatformUrl)
            supportPlatformUrl.SetPlatformUrl(platformUrl);

        var rri = new DynamicRenderPageInfo()
        {
            RootId = rootId,
            Page = page,
            ModelView = view,
            PlatformUrl = platformUrl,
            Model = dm,
            Template = templateText
        };
        return new AppRuntimeResult(dm, await _dynamicRenderer.RenderPage(rri));
    }
}

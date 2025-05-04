// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Xaml.DynamicRendrer;
using A2v10.Xaml;


namespace A2v10.Metadata;

internal class ReportEndpointBuilder(IServiceProvider _serviceProvider, IModelBuilder _baseBuilder) : IMetaEndpointBuilder
{
    private readonly AppMetadata _appMeta = _baseBuilder.AppMeta;
    private readonly DynamicRenderer _dynamicRenderer = new(_serviceProvider);

    public async Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload)
    {
        var _source = _baseBuilder.Table;
        var _report = _baseBuilder.BaseTable
            ?? throw new InvalidOperationException("Report is null");

        var reportBuilder = _report?.Type switch
        {
            "turnover" => new TurnoverReportBuilder(_serviceProvider, _report, _source),
            _ => throw new NotImplementedException(_report?.Type)
        };

        var dm = await reportBuilder.LoadReportModelAsync(view,  platformUrl.Query ?? new ExpandoObject());

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

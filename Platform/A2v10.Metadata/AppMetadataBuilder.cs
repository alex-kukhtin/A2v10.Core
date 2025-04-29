// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal class AppMetadataBuilder(IServiceProvider _serviceProvider,
    DatabaseMetadataProvider _metadataProvider,
    IModelBuilderFactory _modelBuilderFactory,
    IAppVersion _appVersion) : IAppRuntimeBuilder
{
    public bool IsAutoSupported => false;
    public Boolean IsMetaSupported => true;

    public String MetadataScripts(String minify)
    {
        return $"""<script type="text/javascript" src="/scripts/meta/formdesigner.{minify}js?v={_appVersion.AppVersion}"></script>""";
    }
    public String MetadataStyles(String minify)
    {
        return $"""<link rel="stylesheet" href="/css/meta/formdesigner.{minify}css?v={_appVersion.AppVersion}\">""";
    }

    public Task<EndpointTableInfo> ModelInfoFromPathAsync(String path)
    {
        return _metadataProvider.GetModelInfoFromPathAsync(path);
    }
    public async Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload)
    {
        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, view);

        var endpointBuilder = FindEndpointBuilder(iBuilder.MetadataEndpointBuilder, iBuilder);
        if (endpointBuilder != null)
              return await endpointBuilder.RenderAsync(platformUrl, view, isReload);

        var dm = await iBuilder.LoadModelAsync();

        if (isReload)
            return new AppRuntimeResult(dm, null);
        var page = await iBuilder.RenderPageAsync(view, dm);
        return new AppRuntimeResult(dm, page);
    }

    public async Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms)
    {
        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, view);
        return await iBuilder.SaveModelAsync(data, savePrms);
    }

    public async Task DbRemoveAsync(IPlatformUrl platformUrl, IModelView view, string? propName, ExpandoObject execPrms)
    {
        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, view);
        throw new NotImplementedException();
    }

    public async Task<IInvokeResult> InvokeAsync(IPlatformUrl platformUrl, String command, IModelCommand cmd, ExpandoObject? prms)
    {
        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, cmd);
        return await iBuilder.InvokeAsync(cmd, command, prms);
    }
    public Task<IDataModel> ExecuteCommandAsync(IModelCommand command, ExpandoObject parameters)
    {
        throw new NotImplementedException();
    }

    public async Task<IDataModel> ExpandAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject execPrms)
    {
        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, view);
        throw new NotImplementedException();
    }

    private IMetaEndpointBuilder? FindEndpointBuilder(String? name, IModelBuilder baseBuilder)
    {
        if (String.IsNullOrEmpty(name))
            return null;
        return name switch
        {
            "rep:report.render" => new ReportEndpointBuilder(_serviceProvider, baseBuilder),
            _ => throw new InvalidOperationException("IMetaEndpointBuilder for {name} not found")
        };
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* Eight entry points, one shape each: get the builder, ask it. The factory decides what kind of
 * builder that is, and the builder decides whether it serves the question - so nothing here knows
 * the kinds of endpoint, and nothing here constructs a builder. Which is why the service provider
 * is gone from this class: it was only ever here to 'new' three of them.
 */
internal class AppMetadataBuilder(
    DatabaseMetadataProvider _metadataProvider,
    IModelBuilderFactory _modelBuilderFactory,
    IAppVersion _appVersion) : IAppRuntimeBuilder
{
    public bool IsAutoSupported => false;
    public Boolean IsMetaSupported => true;

    public String MetadataScripts(String minify)
    {
        return $"""
            <script type="text/javascript" src="/scripts/meta/a2v10spreadsheet.{minify}js?v={_appVersion.AppVersion}"></script>
            """;
    }
    public String MetadataStyles(String minify)
    {
        return $"""
            <link rel="stylesheet" href="/css/meta/a2v10spreadsheet.{minify}css?v={_appVersion.AppVersion}\">
            """;
    }

    public Task<EndpointTableInfo> ModelInfoFromPathAsync(String path)
    {
        return _metadataProvider.GetModelInfoFromPathAsync(path);
    }
    public async Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload)
    {
        await _metadataProvider.CheckDeployAsync(view.DataSource);

        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, view);
        return await iBuilder.RenderAsync(view, isReload);
    }

    public async Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms)
    {
        await _metadataProvider.CheckDeployAsync(view.DataSource);
        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, view);
        return await iBuilder.SaveModelAsync(data, savePrms);
    }

    public async Task DbRemoveAsync(IPlatformUrl platformUrl, IModelView view, String? propName, ExpandoObject execPrms)
    {
        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, view);
        await iBuilder.DbRemoveAsync(propName, execPrms);
    }

    public async Task<IInvokeResult> InvokeAsync(IPlatformUrl platformUrl, String command, IModelCommand cmd, ExpandoObject? prms)
    {
        await _metadataProvider.CheckDeployAsync(cmd.DataSource);
        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, cmd);
        return await iBuilder.InvokeAsync(cmd, command, prms);
    }
    public Task<IDataModel> ExecuteCommandAsync(IModelCommand command, ExpandoObject parameters)
    {
        throw new NotImplementedException();
    }

    public async Task<IDataModel> ExpandAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject execPrms)
    {
        await _metadataProvider.CheckDeployAsync(view.DataSource);
        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, view);
        return await iBuilder.ExpandAsync(execPrms);
    }
    public async Task<ExpandoObject> LoadLazyAsync(IPlatformUrl platformUrl, IModelView view)
    {
        await _metadataProvider.CheckDeployAsync(view.DataSource);
        var iBuilder = await _modelBuilderFactory.BuildAsync(platformUrl, view);
        var dm = await  iBuilder.LoadLazyModelAsync();
        return dm.Root;
    }

}

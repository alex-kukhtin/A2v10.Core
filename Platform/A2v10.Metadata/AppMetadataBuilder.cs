// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

public class AppMetadataBuilder(IServiceProvider _serviceProvider,
    IDbContext _dbContext, ICurrentUser _currentUser, DatabaseMetadataProvider _metadataProvider, IAppVersion _appVersion) : IAppRuntimeBuilder
{
    private readonly DatabaseModelProcessor _dbProcessor = new DatabaseModelProcessor(_metadataProvider, _currentUser, _dbContext);
    private readonly ModelPageBuilder _modelPageBuilder = new(_serviceProvider);
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
        if (view.Meta == null)
            throw new InvalidOperationException("meta is null");
        var (dm, meta) = await _dbProcessor.LoadModelAsync(view, platformUrl);
        if (isReload)
            return new AppRuntimeResult(dm, null);
        var page = await _modelPageBuilder.RenderPageAsync(platformUrl, view, dm, meta);
        return new AppRuntimeResult(dm, page);
    }

    public async Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms)
    {
        return await _dbProcessor.SaveModelAsync(view, data, savePrms);
    }

    public Task DbRemoveAsync(IPlatformUrl platformUrl, IModelView view, string? propName, ExpandoObject execPrms)
    {
        throw new NotImplementedException();
    }

    public Task<IDataModel> ExecuteCommandAsync(IModelCommand command, ExpandoObject parameters)
    {
        throw new NotImplementedException();
    }

    public Task<IDataModel> ExpandAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject execPrms)
    {
        throw new NotImplementedException();
    }
}

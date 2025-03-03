// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata.SqlServer;

public class AppMetadataBuilder(IServiceProvider _serviceProvider,
    IDbContext _dbContext, ICurrentUser _currentUser, DatabaseMetadataProvider _metadataProvider) : IAppRuntimeBuilder
{
    private readonly DatabaseModelProcessor _dbProcessor = new DatabaseModelProcessor(_metadataProvider, _currentUser, _dbContext);
    private readonly ModelPageBuilder _modelPageBuilder = new(_serviceProvider);
    public bool IsAutoSupported => false;

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

    public Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms)
    {
        throw new NotImplementedException();
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

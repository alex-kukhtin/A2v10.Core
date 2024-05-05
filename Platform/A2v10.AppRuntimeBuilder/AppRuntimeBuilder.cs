// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.AppRuntimeBuilder;

public class AppRuntimeBuilder(IServiceProvider _serviceProvider,
	RuntimeMetadataProvider _runtimeMetaProvider, IDbContext _dbContext, ICurrentUser _currentUser) : IAppRuntimeBuilder
{
	private readonly SqlModelProcessor _dbProcessor = new(_currentUser, _dbContext);
	private readonly ModelPageBuilder _modelPageBuilder = new(_serviceProvider);
	public Boolean IsAutoSupported => true;

	public async Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload)
	{
		var endpoint = await GetEndpoint(view);

		var dm = await _dbProcessor.LoadModelAsync(platformUrl, view, endpoint);

		if (isReload)
			return new AppRuntimeResult(dm, null);

		var page = await _modelPageBuilder.RenderPageAsync(platformUrl, view, endpoint, dm);

		return new AppRuntimeResult(dm, page);
	}

	async Task<EndpointDescriptor> GetEndpoint(IModelBase modelBase)
	{
		var meta = await _runtimeMetaProvider.GetMetadata();
		return meta.GetEndpoint(modelBase.Path);
	}

	public async Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms)
	{
		var endpoint = await GetEndpoint(view);
		return await _dbProcessor.SaveAsync(endpoint, data);
	}

	public async Task<IDataModel> ExecuteCommandAsync(IModelCommand command, ExpandoObject parameters)
	{
		var endpoint = await GetEndpoint(command);
		return await _dbProcessor.ExecuteCommandAsync(command.LoadProcedure(), endpoint, parameters);
	}

	public async Task DbRemoveAsync(IPlatformUrl platformUrl, IModelView view, String? propName, ExpandoObject execPrms)
	{
		var endpoint = await GetEndpoint(view);
		await _dbProcessor.DbRemoveAsync(propName, endpoint, execPrms);
	}

	public Task<IDataModel> ExpandAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject execPrms)
	{
		throw new NotImplementedException("AppRuntimeBuilder.ExpandAsync yet not implemented");
	}
}

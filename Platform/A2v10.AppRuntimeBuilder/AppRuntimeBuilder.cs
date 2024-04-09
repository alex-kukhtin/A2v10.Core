// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;

using Microsoft.Extensions.Options;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.AppRuntimeBuilder;

public class AppRuntimeBuilder(IServiceProvider _serviceProvider, IOptions<AppOptions> _appOptions,
	RuntimeMetadataProvider _runtimeMetaProvider, IDbContext _dbContext, ICurrentUser _currentUser) : IAppRuntimeBuilder
{
	private readonly SqlModelProcessor _dbProcessor = new(_currentUser, _appOptions, _dbContext);
	private readonly ModelPageBuilder _modelPageBuilder = new(_serviceProvider);
	public Boolean IsAutoSupported => true;

	public async Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload)
	{
		var table = await GetModelTable(view);

		var dm = await _dbProcessor.LoadModelAsync(platformUrl, view, table);

		if (isReload)
			return new AppRuntimeResult(dm, null);

		var page = await _modelPageBuilder.RenderPageAsync(platformUrl, view, table, dm);

		return new AppRuntimeResult(dm, page);
	}

	async Task<RuntimeTable> GetModelTable(IModelBase modelBase)
	{
		var meta = await _runtimeMetaProvider.GetMetadata();
		var cm = modelBase.CurrentModel
			?? throw new InvalidOperationException($"{modelBase.CurrentModel} is null");

		return meta.GetTable(cm)
			?? throw new InvalidOperationException($"Table {modelBase.CurrentModel} not found");
	}

	public async Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms)
	{
		var table = await GetModelTable(view);
		return await _dbProcessor.SaveAsync(table, data);
	}

	public async Task<IDataModel> ExecuteCommandAsync(IModelCommand command, ExpandoObject parameters)
	{
		var mt = await GetModelTable(command);
		return await _dbProcessor.ExecuteCommandAsync(command.LoadProcedure(), mt, parameters);
	}
}

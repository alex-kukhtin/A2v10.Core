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
	public async Task<IAppRuntimeResult> RenderAsync(IPlatformUrl platformUrl, IModelView view, bool isReload)
	{
		var table = await GetModelTable(view);

		var dm = await _dbProcessor.LoadModelAsync(platformUrl, view, table);

		if (isReload)
			return new AppRuntimeResult(dm, null);

		var page = await _modelPageBuilder.RenderPageAsync(platformUrl, view, table, dm);

		return new AppRuntimeResult(dm, page);
	}

	async Task<RuntimeTable> GetModelTable(IModelView view)
	{
		var meta = await _runtimeMetaProvider.GetMetadata();
		var cm = view.CurrentModel
			?? throw new InvalidOperationException($"{view.CurrentModel} is null");

		return meta.GetTable(cm)
			?? throw new InvalidOperationException($"Table {view.CurrentModel} not found");
	}

	public async Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView view, ExpandoObject data, ExpandoObject savePrms)
	{
		var table = await GetModelTable(view);
		return await _dbProcessor.SaveAsync(table, data);
	}
}

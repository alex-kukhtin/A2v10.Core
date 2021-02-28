// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Services
{
	public class DataService : IDataService
	{
		private readonly IModelJsonReader _modelReader;
		private readonly IDbContext _dbContext;
		private readonly IUserStateManager _userStateManager;

		public DataService(IModelJsonReader modelReader, IDbContext dbContext, IUserStateManager userStateManager)
		{
			_modelReader = modelReader;
			_dbContext = dbContext;
			_userStateManager = userStateManager;
		}

		public async Task<String> Expand(String baseUrl, Object Id, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = new PlatformUrl(baseUrl);
			var view = await _modelReader.GetViewAsync(platformBaseUrl);
			var expandProc = view.ExpandProcedure();

			ExpandoObject execPrms = new ExpandoObject();
			execPrms.Append(platformBaseUrl.Query);
			setParams?.Invoke(execPrms);
			execPrms.Set("Id", Id);
			execPrms.Append(view.Parameters);

			var model = await _dbContext.LoadModelAsync(view.DataSource, expandProc, execPrms);
			return JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings);
		}

		public async Task<String> Reload(String baseUrl, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = new PlatformUrl(baseUrl);
			var view = await _modelReader.GetViewAsync(platformBaseUrl);
			var loadProc = view.LoadProcedure();

			ExpandoObject execPrms = new ExpandoObject();
			setParams?.Invoke(execPrms);

			throw new NotImplementedException();
		}

		public async Task<String> LoadLazy(String baseUrl, Object Id, String propertyName, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = new PlatformUrl(baseUrl);
			var view = await _modelReader.GetViewAsync(platformBaseUrl);

			String loadProc = view.LoadLazyProcedure(propertyName.ToPascalCase());

			ExpandoObject execPrms = new ExpandoObject();
			execPrms.Append(platformBaseUrl.Query);
			setParams?.Invoke(execPrms);
			execPrms.Set("Id", Id);

			var model = await _dbContext.LoadModelAsync(view.DataSource, loadProc, execPrms);
			return JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings);
		}

		public async Task<String> Save(String baseUrl, ExpandoObject data, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = new PlatformUrl(baseUrl);
			var view = await _modelReader.GetViewAsync(platformBaseUrl);

			ExpandoObject savePrms = new ExpandoObject();
			setParams?.Invoke(savePrms);
			savePrms.Append(view.Parameters);
			CheckUserState(savePrms);
			
			// TODO: HookHandler, invokeTarget, events

			var model = await _dbContext.SaveModelAsync(view.DataSource, view.UpdateProcedure(), data, savePrms);
			return JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings);
		}

		void CheckUserState(ExpandoObject prms)
		{
			if (_userStateManager == null)
				return;
			Int64 userId = prms.Get<Int64>("UserId");
			if (_userStateManager.IsReadOnly(userId))
				throw new DataServiceException("UI:@[Error.DataReadOnly]");
		}
	}
}

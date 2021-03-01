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
	public record DataLoadResult : IDataLoadResult
	{
		public IDataModel Model { get; init; }
		public IModelView View { get; init; }
	}

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

		public Task<IDataLoadResult> Load(UrlKind kind, String baseUrl, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = new PlatformUrl(kind, baseUrl);
			return Load(platformBaseUrl, setParams);
		}

		public Task<IDataLoadResult> Load(String baseUrl, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = new PlatformUrl(baseUrl);
			return Load(platformBaseUrl, setParams);
		}

		async Task<IDataLoadResult> Load(IPlatformUrl platformUrl, Action<ExpandoObject> setParams)
		{
			var view = await _modelReader.GetViewAsync(platformUrl);

			var loadPrms = new ExpandoObject();
			setParams?.Invoke(loadPrms);
			loadPrms.Append(platformUrl.Query);
			loadPrms.AppendIfNotExists(view.Parameters);
			loadPrms.SetNotNull("Id", platformUrl.Id);

			String loadProc = view.HasModel() ? view.LoadProcedure() : null;

			IDataModel model = null;

			if (loadProc != null)
			{
				ExpandoObject prms2 = loadPrms;
				if (view.Indirect)
				{
					// for indirect - @TenantId, @UserId and @Id only
					prms2 = new ExpandoObject();
					prms2.SetNotNull("Id", platformUrl.Id);
					if (loadPrms != null)
					{
						prms2.Set("UserId", loadPrms.Get<Int64>("UserId"));
						prms2.Set("TenantId", loadPrms.Get<Int32>("TenantId"));
					}
				}
				model = await _dbContext.LoadModelAsync(view.DataSource, loadProc, prms2);
				if (view.Merge != null)
				{
					var mergeModel = await _dbContext.LoadModelAsync(view.Merge.DataSource, view.Merge.LoadProcedure(), prms2);
					model.Merge(mergeModel);
				}

				if (view.Copy)
					model.MakeCopy();

				if (platformUrl.Id != null && !view.Copy)
				{
					// check Main Element
					var me = model.MainElement;
					if (me.Metadata != null)
					{
						var modelId = me.Id ?? String.Empty;
						if (platformUrl.Id != modelId.ToString())
							throw new DataServiceException($"Main element not found. Id={platformUrl.Id}");
					}
				}
			}
			if (view.Indirect)
				view = await LoadIndirect(view, model, loadPrms);

			if (model?.Root != null)
			{
				// side effect!
				//rw.view = model.Root.Resolve(rw.View);
				//rw.viewMobile = model.Root.Resolve(rw.viewMobile);
				//rw.template = model.Root.Resolve(rw.template);
				//rw.checkTypes = model.Root.Resolve(rw.checkTypes);
			}

			SetReadOnly(model, loadPrms);

			//dmv.Model = model;
			//dmv.RequestView = rw;
			//return dmv;

			return new DataLoadResult() {
				Model = model,
				View = view
			};
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
			var result = await Load(baseUrl, setParams);
			if (result.Model != null)
				return JsonConvert.SerializeObject(result.Model.Root, JsonHelpers.DataSerializerSettings);
			return "{}";
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

		void SetReadOnly(IDataModel model, ExpandoObject loadPrms)
		{
			if (_userStateManager == null || model == null)
				return;
			Int64 userId = loadPrms.Get<Int64>("UserId");
			if (_userStateManager.IsReadOnly(userId))
				model.SetReadOnly();
		}

		Task<IModelView> LoadIndirect(IModelView view, IDataModel innerModel, ExpandoObject loadPrms)
		{
			/*
			if (!view.Indirect)
				return view;
			if (!String.IsNullOrEmpty(rw.Target))
			{
				String targetUrl = innerModel.Root.Resolve(rw.Target);
				if (String.IsNullOrEmpty(rw.targetId))
					throw new DataServiceException("targetId must be specified for indirect action");
				targetUrl += "/" + innerModel.Root.Resolve(rw.TargetId);
				var platformTargetUrl = new PlatformUrl(targetUrl);

				var rm = await RequestModel.CreateFromUrl(_codeProvider, rw.CurrentKind, targetUrl);
				rw = rm.GetCurrentAction();
				String loadProc = rw.LoadProcedure;
				if (loadProc != null)
				{
					loadPrms.Set("Id", rw.Id);
					loadPrms.AppendIfNotExists(view.Parameters);

					var newModel = await _dbContext.LoadModelAsync(view.DataSource, loadProc, loadPrms);
					innerModel.Merge(newModel);

					innerModel.System.Set("__indirectUrl__", rm.BaseUrl);
				}
			}
			else
			{
				// simple view/model redirect
				if (rw.targetModel == null)
				{
					throw new DataServiceException("'targetModel' must be specified for indirect action without 'target' property");

				}
				rw.model = innerModel.Root.Resolve(rw.targetModel.model);
				rw.view = innerModel.Root.Resolve(rw.targetModel.view);
				rw.viewMobile = innerModel.Root.Resolve(rw.targetModel.viewMobile);
				rw.schema = innerModel.Root.Resolve(rw.targetModel.schema);
				if (String.IsNullOrEmpty(rw.schema))
					rw.schema = null;
				rw.template = innerModel.Root.Resolve(rw.targetModel.template);
				if (String.IsNullOrEmpty(rw.template))
					rw.template = null;
				String loadProc = rw.LoadProcedure();
				if (loadProc != null)
				{
					loadPrms.Set("Id", rw.Id);
					var newModel = await _dbContext.LoadModelAsync(view.DataSource, loadProc, loadPrms);
					innerModel.Merge(newModel);
				}
			}
			return view;
			*/
			throw new NotImplementedException();
		}

	}
}

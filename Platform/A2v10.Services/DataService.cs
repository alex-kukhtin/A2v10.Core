// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using System.Globalization;

namespace A2v10.Services
{
	public record DataLoadResult : IDataLoadResult
	{
		public IDataModel Model { get; init; }
		public IModelView View { get; init; }
	}

	public class DataService : IDataService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly IModelJsonReader _modelReader;
		private readonly IDbContext _dbContext;
		private readonly ICurrentUser _currentUser;

		public DataService(IServiceProvider serviceProvider, IModelJsonReader modelReader, IDbContext dbContext, ICurrentUser currentUser)
		{
			_serviceProvider = serviceProvider;
			_modelReader = modelReader;
			_dbContext = dbContext;
			_currentUser = currentUser;
		}

		static IPlatformUrl CreatePlatformUrl(UrlKind kind, String baseUrl)
		{
			return new PlatformUrl(kind, baseUrl, null);
		}

		static IPlatformUrl CreatePlatformUrl(String baseUrl, String id = null)
		{
			return new PlatformUrl(baseUrl, id);
		}

		public Task<IDataLoadResult> LoadAsync(UrlKind kind, String baseUrl, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = CreatePlatformUrl(kind, baseUrl);
			return Load(platformBaseUrl, setParams);
		}

		public Task<IDataLoadResult> LoadAsync(String baseUrl, Action<ExpandoObject> setParams)
		{
			// with redirect here only!
			var platformBaseUrl = CreatePlatformUrl(baseUrl, null);
			return Load(platformBaseUrl, setParams);
		}

		async Task<IDataLoadResult> Load(IPlatformUrl platformUrl, Action<ExpandoObject> setParams)
		{
			var view = await _modelReader.GetViewAsync(platformUrl);

			var loadPrms = view.CreateParameters(platformUrl, null, setParams);

			IDataModel model = null;

			if (view.HasModel())
			{
				ExpandoObject prmsForLoad = loadPrms;

				if (view.Indirect)
					prmsForLoad = ParameterBuilder.BuildIndirectParams(platformUrl, setParams);

				model = await _dbContext.LoadModelAsync(view.DataSource, view.LoadProcedure(), prmsForLoad);

				if (view.Merge != null)
				{
					var prmsForMerge = view.Merge.CreateMergeParameters(model, prmsForLoad);
					var mergeModel = await _dbContext.LoadModelAsync(view.Merge.DataSource, view.Merge.LoadProcedure(), prmsForMerge);
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
				view = await LoadIndirect(view, model, setParams);

			if (model != null)
				view = view.Resolve(model);

			SetReadOnly(model, loadPrms);

			return new DataLoadResult()
			{
				Model = model,
				View = view
			};
		}

		public Task<String> ExpandAsync(ExpandoObject queryData, Action<ExpandoObject> setParams)
		{
			var baseUrl = queryData.Get<String>("baseUrl");
			if (baseUrl == null)
				throw new DataServiceException(nameof(ExpandAsync));

			Object id = queryData.Get<Object>("id");
			return ExpandAsync(baseUrl, id, setParams);
		}

		public async Task<String> ExpandAsync(String baseUrl, Object Id, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = CreatePlatformUrl(baseUrl);
			var view = await _modelReader.GetViewAsync(platformBaseUrl);
			var expandProc = view.ExpandProcedure();

			var execPrms = view.CreateParameters(platformBaseUrl, Id, setParams);
			execPrms.SetNotNull("Id", Id);

			var model = await _dbContext.LoadModelAsync(view.DataSource, expandProc, execPrms);
			return JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings);
		}

		public async Task DbRemoveAsync(String baseUrl, Object Id, String propertyName, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = CreatePlatformUrl(baseUrl);
			var view = await _modelReader.GetViewAsync(platformBaseUrl);
			var deleteProc = view.DeleteProcedure(propertyName);

			var execPrms = view.CreateParameters(platformBaseUrl, Id, setParams);
			execPrms.SetNotNull("Id", Id);

			await _dbContext.ExecuteExpandoAsync(view.DataSource, deleteProc, execPrms);
		}

		public async Task<String> ReloadAsync(String baseUrl, Action<ExpandoObject> setParams)
		{
			var result = await LoadAsync(baseUrl, setParams);
			if (result.Model != null)
				return JsonConvert.SerializeObject(result.Model.Root, JsonHelpers.DataSerializerSettings);
			return "{}";
		}

		public Task<String> LoadLazyAsync(ExpandoObject queryData, Action<ExpandoObject> setParams)
		{
			var baseUrl = queryData.Get<String>("baseUrl");
			if (baseUrl == null)
				throw new DataServiceException(nameof(LoadLazyAsync));

			var id = queryData.Get<Object>("id");
			var prop = queryData.Get<String>("prop");
			return LoadLazyAsync(baseUrl, id, prop, setParams);
		}

		public async Task<String> LoadLazyAsync(String baseUrl, Object Id, String propertyName, Action<ExpandoObject> setParams)
		{
			String strId = Id != null ? Convert.ToString(Id, CultureInfo.InvariantCulture) : null;

			var platformBaseUrl = CreatePlatformUrl(baseUrl, strId);
			var view = await _modelReader.GetViewAsync(platformBaseUrl);

			String loadProc = view.LoadLazyProcedure(propertyName.ToPascalCase());
			var loadParams = view.CreateParameters(platformBaseUrl, Id, setParams, IModelBase.ParametersFlags.SkipModelJsonParams);

			var model = await _dbContext.LoadModelAsync(view.DataSource, loadProc, loadParams);

			return JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings);
		}

		public async Task<String> SaveAsync(String baseUrl, ExpandoObject data, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = CreatePlatformUrl(baseUrl);
			var view = await _modelReader.GetViewAsync(platformBaseUrl);

			var savePrms = view.CreateParameters(platformBaseUrl, null, setParams);

			CheckUserState(savePrms);

			// TODO: HookHandler, invokeTarget, events

			var model = await _dbContext.SaveModelAsync(view.DataSource, view.UpdateProcedure(), data, savePrms);
			return JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings);
		}

		public async Task<IInvokeResult> InvokeAsync(String baseUrl, String command, ExpandoObject data, Action<ExpandoObject> setParams)
		{
			var platformBaseUrl = CreatePlatformUrl(baseUrl);
			var cmd = await _modelReader.GetCommandAsync(platformBaseUrl, command);

			var prms = cmd.CreateParameters(platformBaseUrl, null, (eo) =>
				{
					setParams?.Invoke(eo);
					eo.Append(data);
				}, 
				IModelBase.ParametersFlags.SkipId
			);
			setParams?.Invoke(prms);

			var invokeCommand = cmd.GetCommandHandler(_serviceProvider);
			var result = await invokeCommand.ExecuteAsync(cmd, prms);
			//await ProcessDbEvents();
			return result;
		}

		void CheckUserState(ExpandoObject prms)
		{

			if (_currentUser.State.IsReadOnly)
				throw new DataServiceException("UI:@[Error.DataReadOnly]");
		}

		void SetReadOnly(IDataModel model, ExpandoObject loadPrms)
		{
			if (_currentUser.State.IsReadOnly)
				model.SetReadOnly();
		}

		async Task<IModelView> LoadIndirect(IModelView view, IDataModel innerModel, Action<ExpandoObject> setParams)
		{
			if (!view.Indirect)
				return view;

			if (!String.IsNullOrEmpty(view.Target))
			{
				String targetUrl = innerModel.Root.Resolve(view.Target);
				if (String.IsNullOrEmpty(view.TargetId))
					throw new DataServiceException("targetId must be specified for indirect action");
				targetUrl += "/" + innerModel.Root.Resolve(view.TargetId);

				// TODO: CurrentKind instead UrlKind.Page
				var platformUrl = CreatePlatformUrl(UrlKind.Page, targetUrl);
				view = await _modelReader.GetViewAsync(platformUrl);

				//var rm = await RequestModel.CreateFromUrl(_codeProvider, rw.CurrentKind, targetUrl);
				//rw = rm.GetCurrentAction();
				if (view.HasModel())
				{
					// TODO: ParameterBuilder
					var indirectParams = view.CreateParameters(platformUrl, setParams);

					var newModel = await _dbContext.LoadModelAsync(view.DataSource, view.LoadProcedure(), indirectParams);
					innerModel.Merge(newModel);
					throw new NotImplementedException("Full URL is required");
					//innerModel.System.Set("__indirectUrl__", view.BaseUrl);
				}
			}
			else
			{
				// simple view/model redirect
				if (view.TargetModel == null)
					throw new DataServiceException("'targetModel' must be specified for indirect action without 'target' property");
				//TODO: view = view.Resolve(innerModel);
				/*
				rw.model = innerModel.Root.Resolve(view.targetModel.model);
				rw.view = innerModel.Root.Resolve(view.targetModel.view);
				rw.viewMobile = innerModel.Root.Resolve(rw.targetModel.viewMobile);
				rw.schema = innerModel.Root.Resolve(rw.targetModel.schema);
				if (String.IsNullOrEmpty(rw.schema))
					rw.schema = null;
				rw.template = innerModel.Root.Resolve(rw.targetModel.template);
				if (String.IsNullOrEmpty(rw.template))
					rw.template = null;
				*/
				if (view.HasModel())
				{
					//loadPrms.Set("Id", platformUrl.Id);
					//var newModel = await _dbContext.LoadModelAsync(view.DataSource, view.LoadProcedure(), loadPrms);
					//innerModel.Merge(newModel);
				}
			}
			return view;
		}

		public async Task<IBlobInfo> LoadBlobAsync(UrlKind kind, String baseUrl, Action<ExpandoObject> setParams, String suffix = null)
		{
			var platfromUrl = CreatePlatformUrl(kind, baseUrl);
			var blob = await _modelReader.GetBlobAsync(platfromUrl, suffix);
			var prms = new ExpandoObject();
			prms.Set("Id", blob.Id);
			prms.Set("Key", blob.Key);
			setParams?.Invoke(prms);
			var bi = await _dbContext.LoadAsync<BlobInfo>(blob.DataSource, blob.LoadProcedure(), prms);
			if (!String.IsNullOrEmpty(bi.BlobName))
				throw new NotImplementedException("Load azure Storage blob");
			return bi;
		}
	}
}

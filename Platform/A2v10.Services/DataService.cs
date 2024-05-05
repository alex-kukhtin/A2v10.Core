// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Services.Interop;

namespace A2v10.Services;

public record DataLoadResult(IDataModel? Model, IModelView? View, String? ActionResult = null) : IDataLoadResult;

public class LayoutDescription : ILayoutDescription
{
	public LayoutDescription(List<String>? styles, List<String>? scripts)
	{
		if (styles != null && styles.Count > 0)
		{
			var sb = new StringBuilder();
			foreach (var s in styles)
				sb.Append($"<link href=\"{s}\" rel=\"stylesheet\" />\n");
			ModelStyles = sb.ToString();
		}
		if (scripts != null && scripts.Count > 0)
		{
			var sb = new StringBuilder();
			foreach (var s in scripts)
				sb.Append($"<script type=\"text/javascript\" src=\"{s}\"></script>\n");
			ModelScripts = sb.ToString();
		}
	}

	public String? ModelScripts { get; init; }
	public String? ModelStyles { get; init; }
}

public class SaveResult : ISaveResult
{
	public String Data { get; init; } = "{}";
	public ISignalResult? SignalResult { get; init; }

}
public partial class DataService(IServiceProvider _serviceProvider, IModelJsonReader _modelReader, IDbContext _dbContext, ICurrentUser _currentUser,
    ISqlQueryTextProvider _sqlQueryTextProvider, IAppCodeProvider _codeProvider,
    IExternalDataProvider _externalDataProvider, ILocalizer _localizer, IAppRuntimeBuilder _appRuntimeBuilder) : IDataService
{
    static PlatformUrl CreatePlatformUrl(UrlKind kind, String baseUrl)
	{
		return new PlatformUrl(kind, baseUrl, null);
	}

	static PlatformUrl CreatePlatformUrl(String baseUrl, String? id = null)
	{
		return new PlatformUrl(baseUrl, id);
	}

	public Task<IDataLoadResult> LoadAsync(UrlKind kind, String baseUrl, Action<ExpandoObject> setParams, Boolean isReload = false)
	{
		var platformBaseUrl = CreatePlatformUrl(kind, baseUrl);
		return Load(platformBaseUrl, setParams, isReload);
	}

	public Task<IDataLoadResult> LoadAsync(String baseUrl, Action<ExpandoObject> setParams, Boolean isReload = false)
	{
		// with redirect here only!
		var platformBaseUrl = CreatePlatformUrl(baseUrl, null);
		return Load(platformBaseUrl, setParams, isReload);
	}

    public async Task<IInvokeResult> ExportAsync(String baseUrl, Action<ExpandoObject> setParams)
    {
        var platformUrl = CreatePlatformUrl(UrlKind.Page, baseUrl);
        var view = await _modelReader.GetViewAsync(platformUrl);
        if (view.Export == null)
			throw new DataServiceException("model.json. export element not found");
        var loadPrms = view.CreateParameters(platformUrl, null, setParams);
        var dm = await _dbContext.LoadModelAsync(view.DataSource, view.ExportProcedure(), loadPrms);
		var export = view.Export;
        Stream? stream;
        var templExpr = export.GetTemplateExpression();
        if (!String.IsNullOrEmpty(templExpr))
        {
            var bytes = dm.Eval<Byte[]>(templExpr)
                ?? throw new DataServiceException($"Template stream not found or its format is invalid. ({templExpr})");
            stream = new MemoryStream(bytes);
        }
        else if (!String.IsNullOrEmpty(export.Template))
        {
            var fileName = export.Template.AddExtension(export.Format.ToString());
            var pathToRead = _codeProvider.MakePath(view.Path, fileName);
            stream = _codeProvider.FileStreamResource(pathToRead) ??
                throw new DataServiceException($"Template file not found ({fileName})");
        }
        else
			throw new DataServiceException($"Export template not defined");
		var fn = _localizer.Localize(dm.Resolve(export.FileName));

        var resultFileName = $"{fn}.{export.Format}";
		var resultMime = MimeTypes.GetMimeMapping($".{export.Format}");

        switch (export.Format)
		{
			case ModelJsonExportFormat.xlsx:
				{
					var rep = new ExcelReportGenerator(stream);
					rep.GenerateReport(dm);
					if (rep.ResultFile == null)
						throw new DataServiceException("Generate file error");
					var bytes = await File.ReadAllBytesAsync(rep.ResultFile);
					return new InvokeResult(bytes, resultMime, resultFileName);
                }
			case ModelJsonExportFormat.dbf:
			case ModelJsonExportFormat.csv:
				{
					var fmt = export.Format.ToString().ToLowerInvariant();
                    var extDataProvider = _externalDataProvider.GetWriter(dm, fmt, export.GetEncoding())
                        ?? throw new DataServiceException($"There is no data provider for '{fmt}' files");
					using var ms = new MemoryStream();
                    extDataProvider.Write(ms);
					return new InvokeResult(ms.GetBuffer(), resultMime, resultFileName);
                }
			default:
                throw new DataServiceException($"Export not implemented for {export.Format}");
        }
    }

    private void CheckRoles(IModelBase modelBase)
	{
		if (!modelBase.CheckRoles(_currentUser.Identity.Roles))
			throw new DataServiceException("Access denied");
	}

	async Task<IModelView> LoadViewAsync(IPlatformUrl platformUrl)
	{
		var view = await _modelReader.GetViewAsync(platformUrl);
		CheckRoles(view);
		return view;
	}

	void CheckPermissions(IModelBase modelBase)
	{
		if (modelBase.Permissions == null)
			return;
		foreach (var (k, v) in modelBase.Permissions)
		{
			if (!_currentUser.IsPermissionEnabled(k, (PermissionFlag) v))
				throw new DataServiceException("UI:Access denied");
		}
	}

	async Task<IDataLoadResult> Load(IPlatformUrl platformUrl, Action<ExpandoObject> setParams, Boolean isReload = false)
	{
		var view = await LoadViewAsync(platformUrl);

		CheckPermissions(view);

		if (view.ModelAuto != null)
		{
			var result = await _appRuntimeBuilder.RenderAsync(platformUrl, view, isReload);
			return new DataLoadResult(result.DataModel, null, result.ActionResult);
		}
		else if (!String.IsNullOrEmpty(view.EndpointHandler))
		{
			var handler = GetEndpointHandler(view.EndpointHandler);
			var prms = view.CreateParameters(platformUrl, null, setParams);
			if (isReload)
			{
				var dm = await handler.ReloadAsync(platformUrl, view, prms);
				return new DataLoadResult(dm, null, null);
			}
			else
			{
				var result = await handler.RenderResultAsync(platformUrl, view, prms);
				return new DataLoadResult(null, null, result);
			}
		}

		var loadPrms = view.CreateParameters(platformUrl, null, setParams);

		IDataModel? model = null;

		if (view.HasModel())
		{
			ExpandoObject prmsForLoad = loadPrms;

			if (view.Indirect)
				prmsForLoad = ParameterBuilder.BuildIndirectParams(platformUrl, setParams);

			var sqlTextKey = view.SqlTextKey();
			if (sqlTextKey == null)
				model = await _dbContext.LoadModelAsync(view.DataSource, view.LoadProcedure(), prmsForLoad);
			else
			{
				var sqlText = _sqlQueryTextProvider.GetSqlText(sqlTextKey, prmsForLoad);
				model = await _dbContext.LoadModelSqlAsync(view.DataSource, sqlText, prmsForLoad);
			}

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
		{
			view = view.Resolve(model);
			SetReadOnly(model);
		}

		return new DataLoadResult
		(
			Model: model,
			View: view
		);
	}

	public Task<String> ExpandAsync(ExpandoObject queryData, Action<ExpandoObject> setParams)
	{
		var baseUrl = queryData.Get<String>("baseUrl") 
			?? throw new DataServiceException(nameof(ExpandAsync));
		Object? id = queryData.Get<Object>("id");
		return ExpandAsync(baseUrl, id, setParams);
	}

	public async Task<String> ExpandAsync(String baseUrl, Object? Id, Action<ExpandoObject> setParams)
	{
		var platformBaseUrl = CreatePlatformUrl(baseUrl);
		var view = await LoadViewAsync(platformBaseUrl);
		var expandProc = view.ExpandProcedure();

		var execPrms = view.CreateParameters(platformBaseUrl, Id, setParams);
		execPrms.SetNotNull("Id", Id);

		IDataModel? model;
		if (view.ModelAuto != null)
			model = await _appRuntimeBuilder.ExpandAsync(platformBaseUrl, view, execPrms);
		else
			model = await _dbContext.LoadModelAsync(view.DataSource, expandProc, execPrms);

		return JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings);
	}

	public async Task DbRemoveAsync(String baseUrl, Object Id, String? propertyName, Action<ExpandoObject> setParams)
	{
		var platformBaseUrl = CreatePlatformUrl(baseUrl);
		var view = await LoadViewAsync(platformBaseUrl);

		var execPrms = view.CreateParameters(platformBaseUrl, Id, setParams);
		execPrms.SetNotNull("Id", Id);

		var deleteProc = view.DeleteProcedure(propertyName);

		if (view.ModelAuto != null)
			await _appRuntimeBuilder.DbRemoveAsync(platformBaseUrl, view, propertyName, execPrms);
		else
			await _dbContext.ExecuteExpandoAsync(view.DataSource, deleteProc, execPrms);
	}

	public async Task<String> ReloadAsync(String baseUrl, Action<ExpandoObject> setParams)
	{
		var result = await LoadAsync(baseUrl, setParams, true);
		if (result.Model != null)
			return JsonConvert.SerializeObject(result.Model.Root, JsonHelpers.DataSerializerSettings);
		return "{}";
	}

	public Task<String> LoadLazyAsync(ExpandoObject queryData, Action<ExpandoObject> setParams)
	{
		var baseUrl = queryData.Get<String>("baseUrl") 
			?? throw new DataServiceException(nameof(LoadLazyAsync));
		var id = queryData.Get<Object>("id");
		var prop = queryData.GetNotNull<String>("prop");
		return LoadLazyAsync(baseUrl, id, prop, setParams);
	}

	public async Task<String> LoadLazyAsync(String baseUrl, Object? Id, String propertyName, Action<ExpandoObject> setParams)
	{
		String? strId = Id != null ? Convert.ToString(Id, CultureInfo.InvariantCulture) : null;

		var platformBaseUrl = CreatePlatformUrl(baseUrl, strId);
		var view = await LoadViewAsync(platformBaseUrl);

		String loadProc = view.LoadLazyProcedure(propertyName.ToPascalCase());
		var loadParams = view.CreateParameters(platformBaseUrl, Id, setParams, IModelBase.ParametersFlags.SkipModelJsonParams);

		var model = await _dbContext.LoadModelAsync(view.DataSource, loadProc, loadParams);

		return JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings);
	}

	static void ResolveParams(ExpandoObject prms, ExpandoObject data)
	{
		if (prms == null || data == null)
			return;
		var vals = new Dictionary<String, String?>();
		foreach (var (k, v) in prms)
		{
			if (v != null && v is String strVal && strVal.StartsWith("{{"))
			{
				vals.Add(k, data.Resolve(strVal));
			}
		}
		foreach (var (k, v) in vals)
		{
			prms.Set(k, v);
		}
	}

	private IEndpointHandler GetEndpointHandler(String endpoint)
	{
		var handlerKeys = endpoint.Split(':');
		if (handlerKeys.Length != 2)
			throw new InvalidOperationException("Invalid EndpointHandler");
		return _serviceProvider.GetRequiredKeyedService<IEndpointHandler>(handlerKeys[0]);

	}
	public async Task<ISaveResult> SaveAsync(String baseUrl, ExpandoObject data, Action<ExpandoObject> setParams)
	{
		var platformBaseUrl = CreatePlatformUrl(baseUrl);
		var view = await LoadViewAsync(platformBaseUrl);

        if (_currentUser.State.IsReadOnly)
			throw new DataServiceException("UI:Access denied");

		CheckPermissions(view);

		var savePrms = view.CreateParameters(platformBaseUrl, null, setParams);

		ResolveParams(savePrms, data);

		CheckUserState();

		if (view.ModelAuto != null)
		{
			var saveResult = await _appRuntimeBuilder.SaveAsync(platformBaseUrl, view, data, savePrms);
			return new SaveResult()
			{
				Data = JsonConvert.SerializeObject(saveResult, JsonHelpers.DataSerializerSettings),
			};

		}
		else if (!String.IsNullOrEmpty(view.EndpointHandler))
		{
			var handler = GetEndpointHandler(view.EndpointHandler);
			var saveResult = await handler.SaveAsync(platformBaseUrl, view, data, savePrms);
			return new SaveResult()
			{
				Data = JsonConvert.SerializeObject(saveResult, JsonHelpers.DataSerializerSettings),
			};
		}

		// TODO: HookHandler, invokeTarget, events

		var model = await _dbContext.SaveModelAsync(view.DataSource, view.UpdateProcedure(), data, savePrms);

		ISignalResult? signalResult = null;
		if (view.Signal)
		{
			var signal = model.Root.Get<ExpandoObject>("Signal");
			model.Root.Set("Signal", null);
			if (signal != null)
				signalResult = SignalResult.FromData(signal);
		}

		var result = new SaveResult()
		{
			Data = JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings),
			SignalResult = signalResult
		};
		return result;
	}

	public async Task<IInvokeResult> InvokeAsync(String baseUrl, String command, ExpandoObject? data, Action<ExpandoObject> setParams)
	{
		var platformBaseUrl = CreatePlatformUrl(baseUrl);
		var cmd = await _modelReader.GetCommandAsync(platformBaseUrl, command);

		CheckPermissions(cmd);

		CheckRoles(cmd);


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

	void CheckUserState()
	{
		if (_currentUser.State.IsReadOnly)
			throw new DataServiceException("UI:@[Error.DataReadOnly]");
	}

	void SetReadOnly(IDataModel model)
	{
		if (_currentUser.State.IsReadOnly)
			model.SetReadOnly();
	}

	async Task<IModelView> LoadIndirect(IModelView view, IDataModel? innerModel, Action<ExpandoObject> setParams)
	{
		if (!view.Indirect || innerModel == null)
			return view;

		if (!String.IsNullOrEmpty(view.Target))
		{
			String? targetUrl = innerModel.Root.Resolve(view.Target);
			if (String.IsNullOrEmpty(view.TargetId))
				throw new DataServiceException("targetId must be specified for indirect action");
			targetUrl += "/" + innerModel.Root.Resolve(view.TargetId);

			// TODO: CurrentKind instead UrlKind.Page
			var platformUrl = CreatePlatformUrl(UrlKind.Page, targetUrl);
			view = await LoadViewAsync(platformUrl);

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

    public async Task<ILayoutDescription?> GetLayoutDescriptionAsync(String? baseUrl)
	{
		if (baseUrl == null)
			return null;
		var platformUrl = CreatePlatformUrl(UrlKind.Page, baseUrl);
		var view = await _modelReader.TryGetViewAsync(platformUrl);
		if (view == null)
			return null;
		if (view.Styles == null && view.Scripts == null)
			return null;
		return new LayoutDescription(view.Styles, view.Scripts);
	}

	public Byte[] Html2Excel(String html)
	{
		var h = new Html2Excel(_currentUser.Locale.Locale);
		return h.ConvertHtmlToExcel(html);
	}
}



using System;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;


namespace A2v10.Core.Web.Mvc.Controllers
{
	[Route("_data/[action]")]
	[ExecutingFilter]
	[Authorize]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class DataController : BaseController
	{
		private readonly IDataService _dataService;

		public DataController(IDbContext dbContext, IApplicationHost host, IAppCodeProvider codeProvider,
			ILocalizer localizer, IUserStateManager userStateManager, IProfiler profiler, IDataService dataService)
			: base(dbContext, host, codeProvider, localizer, userStateManager, profiler)
		{
			_dataService = dataService;
		}

		[HttpPost]
		public async Task Reload()
		{
			try
			{
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);
				var baseUrl = eo.Get<String>("baseUrl");
				if (baseUrl == null)
					throw new InvalidReqestExecption(nameof(Reload));

				String data = await _dataService.Reload(null, SetSqlQueryParams);

				Response.ContentType = MimeTypes.Application.Json;
				await HttpResponseWritingExtensions.WriteAsync(Response, data, Encoding.UTF8);

				// await ReloadData(json, SetSqlQueryParams, Response.Body);
				//await _baseController.Data(command, SetSqlQueryParams, json, Response);
			}
			catch (Exception ex)
			{
				WriteExceptionStatus(ex);
			}
		}

		async Task ReloadData(String json, Action<ExpandoObject> setParams, Stream output)
		{
			ExpandoObject dataFromJson2 = JsonConvert.DeserializeObject<ExpandoObject>(json, new ExpandoObjectConverter());
			String baseUrl = dataFromJson2.Get<String>("baseUrl");

			ExpandoObject loadPrms = new ExpandoObject();
			if (baseUrl.Contains("?"))
			{
				var parts = baseUrl.Split('?');
				baseUrl = parts[0];
				// parts[1] contains query parameters
				var qryParams = HttpUtility.ParseQueryString(parts[1]);
				loadPrms.Append(CheckPeriod(qryParams), toPascalCase: true);
			}

			//if (NormalizeBaseUrl != null)
				//baseUrl = NormalizeBaseUrl(baseUrl);

			if (baseUrl == null)
				throw new RequestModelException("There are not base url for command 'reload'");

			var rm = await RequestModel.CreateFromBaseUrl(_codeProvider, baseUrl);
			RequestView rw = rm.GetCurrentAction();
			String loadProc = rw.LoadProcedure;
			if (loadProc == null)
				throw new RequestModelException("The data model is empty");
			setParams?.Invoke(loadPrms);
			loadPrms.Set("Id", rw.Id);
			loadPrms.Append(rw.parameters);
			ExpandoObject prms2 = loadPrms;
			if (rw.indirect)
			{
				// for indirect action - @UserId and @Id only
				prms2 = new ExpandoObject();
				setParams?.Invoke(prms2);
				prms2.Set("Id", rw.Id);
			}
			IDataModel model = await _dbContext.LoadModelAsync(rw.CurrentSource, loadProc, prms2);
			if (rw.HasMerge)
			{
				var mergeModel = await _dbContext.LoadModelAsync(rw.MergeSource, rw.MergeLoadProcedure, prms2);
				model.Merge(mergeModel);
			}
			rw = await LoadIndirect(rw, model, loadPrms);
			model.AddRuntimeProperties();
			await WriteDataModel(model, output);
		}

		Task WriteDataModel(IDataModel model, Stream output)
		{
			// Write data to output
			var tw = new StreamWriter(output);
			if (model != null)
				return tw.WriteAsync(JsonConvert.SerializeObject(model.Root, JsonHelpers.ConfigSerializerSettings(_host.IsDebugConfiguration)));
			else
				return tw.WriteAsync("{}");
		}

		async Task<RequestView> LoadIndirect(RequestView rw, IDataModel innerModel, ExpandoObject loadPrms)
		{
			if (!rw.indirect)
				return rw;
			if (!String.IsNullOrEmpty(rw.target))
			{
				String targetUrl = innerModel.Root.Resolve(rw.target);
				if (String.IsNullOrEmpty(rw.targetId))
					throw new RequestModelException("targetId must be specified for indirect action");
				targetUrl += "/" + innerModel.Root.Resolve(rw.targetId);
				var rm = await RequestModel.CreateFromUrl(_codeProvider, rw.CurrentKind, targetUrl);
				rw = rm.GetCurrentAction();
				String loadProc = rw.LoadProcedure;
				if (loadProc != null)
				{
					loadPrms.Set("Id", rw.Id);
					if (rw.parameters != null)
						loadPrms.AppendIfNotExists(rw.parameters);
					var newModel = await _dbContext.LoadModelAsync(rw.CurrentSource, loadProc, loadPrms);
					innerModel.Merge(newModel);
					innerModel.System.Set("__indirectUrl__", rm.BaseUrl);
				}
			}
			else
			{
				// simple view/model redirect
				if (rw.targetModel == null)
				{
					throw new RequestModelException("'targetModel' must be specified for indirect action without 'target' property");

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
				String loadProc = rw.LoadProcedure;
				if (loadProc != null)
				{
					loadPrms.Set("Id", rw.Id);
					var newModel = await _dbContext.LoadModelAsync(rw.CurrentSource, loadProc, loadPrms);
					innerModel.Merge(newModel);
				}
			}
			return rw;
		}

		[HttpPost]
		public async Task Expand()
		{
			await TryCatch(async () =>
			{
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);

				var baseUrl = eo.Get<String>("baseUrl");
				if (baseUrl == null)
					throw new InvalidReqestExecption(nameof(Reload));

				Object id = eo.Get<Object>("id");

				var expandData = await _dataService.Expand(baseUrl, id, SetSqlQueryParams);

				Response.ContentType = MimeTypes.Application.Json;
				await HttpResponseWritingExtensions.WriteAsync(Response, expandData, Encoding.UTF8);
			});
		}

		[HttpPost]
		public async Task LoadLazy()
		{
			await TryCatch(async () =>
			{
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);

				var baseUrl = eo.Get<String>("baseUrl");
				if (baseUrl == null)
					throw new InvalidReqestExecption(nameof(LoadLazy));

				var id = eo.Get<Object>("id");
				var prop = eo.Get<String>("prop");

				var lazyData = await _dataService.LoadLazy(baseUrl, id, prop, SetSqlQueryParams);

				Response.ContentType = MimeTypes.Application.Json;
				await HttpResponseWritingExtensions.WriteAsync(Response, lazyData, Encoding.UTF8);
			});
		}

		[HttpPost]
		public Task Save()
		{
			return TryCatch(async () =>
			{
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);
				String baseUrl = eo.Get<String>("baseUrl");
				if (baseUrl == null)
					throw new InvalidReqestExecption(nameof(Save));
				ExpandoObject data = eo.Get<ExpandoObject>("data");

				var savedData = await _dataService.Save(baseUrl, data, SetSqlQueryParams);

				Response.ContentType = MimeTypes.Application.Json;
				await HttpResponseWritingExtensions.WriteAsync(Response, savedData, Encoding.UTF8);
			});
		}

		[HttpPost]
		public IActionResult Invoke()
		{
			return Content("Expand HERE");
		}

		[HttpPost]
		public IActionResult DbRemove()
		{
			return Content("DbRemove");
		}

		[HttpPost]
		public IActionResult ExportTo()
		{
			return Content("ExportTo");
		}

		private async Task TryCatch(Func<Task> action)
		{
			try
			{
				await action();
			} 
			catch (Exception ex)
			{
				Response.StatusCode = 500;
				await HttpResponseWritingExtensions.WriteAsync(Response, ex.Message, Encoding.UTF8);
			}
		}
	}
}

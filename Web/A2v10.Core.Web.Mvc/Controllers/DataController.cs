
using System;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Core.Web.Mvc.Controllers
{
	[Route("_data/[action]")]
	[ExecutingFilter]
	public class DataController : BaseController
	{
		public DataController(IDbContext dbContext, IApplicationHost host, IAppCodeProvider codeProvider,
			ILocalizer localizer, IUserStateManager userStateManager, IProfiler profiler)
			: base(dbContext, host, codeProvider, localizer, userStateManager, profiler)
		{
		}

		[HttpPost]
		public async Task Reload()
		{
			Response.ContentType = "application/json";
			try
			{
				using var tr = new StreamReader(Request.Body);
				String json = await tr.ReadToEndAsync();
				await ReloadData(json, SetSqlQueryParams, Response.Body);
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

		public IActionResult Save()
		{
			return Content("SAVE HERE");
		}

		public IActionResult Expand()
		{
			return Content("Expand HERE");
		}

		public IActionResult Invoke()
		{
			return Content("Expand HERE");
		}
	}
}

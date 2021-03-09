// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

		public DataController(IApplicationHost host,
			ILocalizer localizer, IUserStateManager userStateManager, IProfiler profiler, IDataService dataService)
			: base(host, localizer, userStateManager, profiler)
		{
			_dataService = dataService;
		}

		[HttpPost]
		public async Task Reload()
		{
			await TryCatch(async () =>
			{ 
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);
				var baseUrl = eo.Get<String>("baseUrl");
				if (baseUrl == null)
					throw new InvalidReqestExecption(nameof(Reload));

				String data = await _dataService.ReloadAsync(baseUrl, SetSqlQueryParams);
				Response.ContentType = MimeTypes.Application.Json;
				await HttpResponseWritingExtensions.WriteAsync(Response, data, Encoding.UTF8);
			});
		}

		[HttpPost]
		public Task Expand()
		{
			return TryCatch(async () =>
			{
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);

				var baseUrl = eo.Get<String>("baseUrl");
				if (baseUrl == null)
					throw new InvalidReqestExecption(nameof(Expand));

				Object id = eo.Get<Object>("id");

				var expandData = await _dataService.ExpandAsync(baseUrl, id, SetSqlQueryParams);

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

				var lazyData = await _dataService.LoadLazyAsync(baseUrl, id, prop, SetSqlQueryParams);

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

				var savedData = await _dataService.SaveAsync(baseUrl, data, SetSqlQueryParams);

				Response.ContentType = MimeTypes.Application.Json;
				await HttpResponseWritingExtensions.WriteAsync(Response, savedData, Encoding.UTF8);
			});
		}

		[HttpPost]
		public Task Invoke()
		{
			return TryCatch(async () =>
			{
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);
				String baseUrl = eo.Get<String>("baseUrl");
				if (baseUrl == null)
					throw new InvalidReqestExecption(nameof(Invoke));
				String cmd = eo.Get<String>("cmd");
				if (cmd == null)
					throw new InvalidReqestExecption(nameof(Invoke));
				ExpandoObject data = eo.Get<ExpandoObject>("data");

				var result = await _dataService.InvokeAsync(baseUrl, cmd, data, SetSqlQueryParams);
				Response.ContentType = result.ContentType;
				await HttpResponseWritingExtensions.WriteAsync(Response, result.Body, Encoding.UTF8);
			});
		}

		[HttpPost]
		public Task DbRemove()
		{
			return TryCatch(async () =>
			{
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);

				var baseUrl = eo.Get<String>("baseUrl");
				if (baseUrl == null)
					throw new InvalidReqestExecption(nameof(DbRemove));

				Object id = eo.Get<Object>("id");
				String propName = eo.Get<String>("prop");

				await _dataService.DbRemoveAsync(baseUrl, id,  propName, SetSqlQueryParams);

				Response.ContentType = MimeTypes.Application.Json;
				await HttpResponseWritingExtensions.WriteAsync(Response, "{\"status\": \"OK\"}", Encoding.UTF8);
			});
		}

		[HttpPost]
		public IActionResult ExportTo()
		{
			throw new NotImplementedException("DataController.ExportTo");
		}

		private async Task TryCatch(Func<Task> action)
		{
			try
			{
				await action();
			} 
			catch (Exception ex)
			{
				await WriteExceptionStatus(ex);
			}
		}
	}
}

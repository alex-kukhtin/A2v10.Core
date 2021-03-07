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

				String data = await _dataService.Reload(baseUrl, SetSqlQueryParams);
				Response.ContentType = MimeTypes.Application.Json;
				await HttpResponseWritingExtensions.WriteAsync(Response, data, Encoding.UTF8);
			});
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
		public async Task<IActionResult> Invoke()
		{
			var eo = await Request.ExpandoFromBodyAsync();
			return Content("Invoke HERE");
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

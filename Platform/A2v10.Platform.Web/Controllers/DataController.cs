// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using A2v10.Infrastructure;


namespace A2v10.Platform.Web.Controllers
{
	[Route("_data/[action]")]
	[Route("admin/_data/[action]")]
	[ExecutingFilter]
	[Authorize]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class DataController : BaseController
	{
		private readonly IDataService _dataService;

		public DataController(IApplicationHost host,
			ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler, IDataService dataService)
			: base(host, localizer, currentUser, profiler)
		{
			_dataService = dataService;
		}

		[HttpPost]
		public async Task<IActionResult> Reload()
		{
			return await TryCatch(async () =>
			{ 
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);
				var baseUrl = eo.Get<String>("baseUrl");
				if (baseUrl == null)
					throw new InvalidReqestExecption(nameof(Reload));

				String data = await _dataService.ReloadAsync(baseUrl, SetSqlQueryParams);
				return new WebActionResult(data);
			});
		}

		[HttpPost]
		public Task<IActionResult> Expand()
		{
			return TryCatch(async () =>
			{
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);

				var expandData = await _dataService.ExpandAsync(eo, SetSqlQueryParams);

				return new WebActionResult(expandData);
			});
		}

		[HttpPost]
		public Task<IActionResult> LoadLazy()
		{
			return TryCatch(async () =>
			{
				var eo = await Request.ExpandoFromBodyAsync();
				if (eo == null)
					throw new InvalidReqestExecption(Request.Path);

				var lazyData = await _dataService.LoadLazyAsync(eo, SetSqlQueryParams);

				return new WebActionResult(lazyData);
			});
		}

		[HttpPost]
		public Task<IActionResult> Save()
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

				return new WebActionResult(savedData);
			});
		}

		[HttpPost]
		public Task<IActionResult> Invoke()
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
				return new WebBinaryActionResult(result.Body, result.ContentType);
			});
		}

		[HttpPost]
		public Task<IActionResult> DbRemove()
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

				return new WebActionResult("{\"status\": \"OK\"}");
			});
		}

		[HttpPost]
		public IActionResult ExportTo()
		{
			throw new NotImplementedException("DataController.ExportTo");
		}

		private async Task<IActionResult> TryCatch(Func<Task<IActionResult>> action)
		{
			try
			{
				return await action();
			} 
			catch (Exception ex)
			{
				return WriteExceptionStatus(ex);
			}
		}
	}
}

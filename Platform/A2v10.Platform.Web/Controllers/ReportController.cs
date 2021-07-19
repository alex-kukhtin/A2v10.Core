// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers
{

	[Route("report/[action]/{Id}")]
	[ExecutingFilter]
	[Authorize]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class ReportController : BaseController
	{
		private readonly IReportService _reportService;

		public ReportController(IApplicationHost host,
			ILocalizer localizer, ICurrentUser currentUser, IUserStateManager userStateManager, IProfiler profiler, IReportService reportService, IUserLocale userLocale)
			: base(host, localizer, currentUser, userStateManager, profiler, userLocale)
		{
			_reportService = reportService;
		}


		[HttpGet]
		public Task Export(String Id, String Base, String Rep, String format = "pdf")
		{
			return TryCatch(async () =>
			{
				var path = Path.Combine(Base, Rep, Id);
				var fmt = Enum.Parse<ExportReportFormat>(format, ignoreCase: true);
				var result = await _reportService.ExportAsync(path + Request.QueryString, fmt, (exp) => {
					exp.SetNotNull("Id", Id);
					SetSqlQueryParams(exp);
				});
				Response.ContentType = result.ContentType;

				var cdh = new ContentDispositionHeaderValue("attachment")
				{
					FileNameStar = Localize(result.FileName)
				};
				Response.Headers.Add("Content-Disposition", cdh.ToString());

				await Response.BodyWriter.WriteAsync(result.Body);
			});
		}

		[HttpGet]
		public Task Print(String Id, String Base, String Rep)
		{
			return TryCatch(async () =>
			{
				var path = Path.Combine(Base, Rep, Id);
				var result = await _reportService.ExportAsync(path + Request.QueryString, ExportReportFormat.Pdf, (exp) => {
					exp.SetNotNull("Id", Id);
					SetSqlQueryParams(exp);
				});
				Response.ContentType = result.ContentType;
				await Response.BodyWriter.WriteAsync(result.Body);
			});
		}

		private async Task TryCatch(Func<Task> action)
		{
			try
			{
				await action();
			}
			catch (Exception ex)
			{
				await WriteHtmlException(ex);
			}
		}
	}
}

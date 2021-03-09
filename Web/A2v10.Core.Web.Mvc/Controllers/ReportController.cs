
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using A2v10.Infrastructure;
using System.IO;
using System.Web;
using System.Net.Http.Headers;

namespace A2v10.Core.Web.Mvc.Controllers
{
	[Route("report/[action]/{Id}")]
	[ExecutingFilter]
	[Authorize]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class ReportController : BaseController
	{
		private readonly IReportService _reportService;

		public ReportController(IApplicationHost host,
			ILocalizer localizer, IUserStateManager userStateManager, IProfiler profiler, IReportService reportService)
			: base(host, localizer, userStateManager, profiler)
		{
			_reportService = reportService;
		}

		[HttpGet]
		public Task Export(String Id, String Base, String Rep, String format = "pdf")
		{
			return TryCatch(async () =>
			{
				var path = Path.Combine(Base, Rep, Id);
				var fmt = (ExportReportFormat)Enum.Parse(typeof(ExportReportFormat), format, ignoreCase: true);
				var result = await _reportService.ExportAsync(path, fmt, (exp) => {
					// TODO: from QueryString
					exp.SetNotNull("Id", Id);
					SetSqlQueryParams(exp);
				});
				Response.ContentType = result.ContentType;

				// TODO: //
				var cdh = new ContentDispositionHeaderValue("attachment")
				{
					FileNameStar = "FILENAME.PDF"//  $"{_localizer.Localize(ri.Name)}.{err.Extension}"
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
				var result = await _reportService.ExportAsync(path, ExportReportFormat.Pdf, (exp) => {
					// TODO: from QueryString
					exp.SetNotNull("Id", Id);
					SetSqlQueryParams(exp);
				});
				Response.ContentType = result.ContentType;
				await Response.BodyWriter.WriteAsync(result.Body);
			});
		}

		// stimulsoft support
		public Task<IActionResult> GetReport()
		{
			throw new NotImplementedException(nameof(GetReport));
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

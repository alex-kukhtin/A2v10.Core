// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers;


public class RenderReportResult
{
	public IActionResult ActionResult { get; }
	public String ContentType { get; }
	public String FileName { get; }
	public RenderReportResult(IActionResult result, String contentType, String fileName)
	{
		ActionResult = result;
		ContentType = contentType;
		FileName = fileName;
	}
}


[Route("report/[action]/{Id}")]
[ExecutingFilter]
[Authorize]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ReportController : BaseController
{
	private readonly IReportService _reportService;

	public ReportController(IApplicationHost host,
		ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler, IReportService reportService)
		: base(host, localizer, currentUser, profiler)
	{
		_reportService = reportService;
	}


	[HttpGet]
	public Task<IActionResult> Show(String Id, String Base, String Rep, String format = "pdf")
	{
		return TryCatch(async () =>
		{

			var res = await Render(Id, Base, Rep, format);
			Response.ContentType = res.ContentType;
			return res.ActionResult;
		});
	}

	[HttpGet]
	public Task<IActionResult> Export(String Id, String Base, String Rep, String format = "pdf")
	{
		return TryCatch(async () =>
		{

			var res = await Render(Id, Base, Rep, format);
			Response.ContentType = res.ContentType;

			var cdh = new ContentDispositionHeaderValue("attachment")
			{
				FileNameStar = Localize(res.FileName)
			};
			Response.Headers.Add("Content-Disposition", cdh.ToString());
			return res.ActionResult;
		});
	}

	[HttpGet]
	public Task<IActionResult> Print(String Id, String Base, String Rep)
	{
		return TryCatch(async () =>
		{
			return (await Render(Id, Base, Rep, "pdf")).ActionResult;
		});
	}

	private async Task<IActionResult> TryCatch(Func<Task<IActionResult>> action)
	{
		try
		{
			return await action();
		}
		catch (Exception ex)
		{
			return WriteHtmlException(ex);
		}
	}

	async Task<RenderReportResult> Render(String Id, String Base, String Rep, String format)
	{
		var path = Path.Combine(Base, Rep, Id);
		var fmt = Enum.Parse<ExportReportFormat>(format, ignoreCase: true);
		var result = await _reportService.ExportAsync(path + Request.QueryString, fmt, (exp) => {
			exp.SetNotNull("Id", Id);
			SetSqlQueryParams(exp);
		});

		var res = new WebBinaryActionResult(result.Body, result.ContentType);
		return new RenderReportResult(res, result.ContentType, result.FileName ?? Rep);
	}
}

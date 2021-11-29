// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Stimulsoft.Report.Mvc;

using A2v10.Infrastructure;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace A2v10.ReportEngine.Stimulsoft.Controllers
{

	[Route("stimulsoft/[action]/{Id}")]
	//[ExecutingFilter]
	[Authorize]
	public class StimulsoftController : Controller
	{

		private readonly IReportService _reportService;
		private readonly StimulsoftReportEngine _reportEngine;
		private readonly ICurrentUser _currentUser;

		public StimulsoftController(IReportService reportService, StimulsoftReportEngine reportEngine, ICurrentUser currentUser)
		{
			_reportService = reportService;
			_reportEngine = reportEngine;
			_currentUser = currentUser;
		}


		public IActionResult Show([FromRoute] String Id, [FromQuery] String Base, [FromQuery] String Rep)
		{
			return View();
		}

		public async Task<IActionResult> GetReport()
		{
			try
			{
				var vrp = StiNetCoreViewer.GetRequestParams(this);
				var rqprms = vrp.HttpContext.Request.Params;
				var Rep = rqprms["rep"];
				var Base = rqprms["base"];
				var Id = vrp.Routes["id"];

				// TODO: QueryString 
				var keys = rqprms.AllKeys.Where(x =>
				{
					var k = x?.ToLowerInvariant() ?? String.Empty;
					return k != "rep" && k != "base" && !k.StartsWith("sti_");
				});
				var queryString = QueryString.Create(keys.Select(k => KeyValuePair.Create(k, rqprms[k])));

				if (Base is null || Rep is null || Id is null)
					throw new InvalidOperationException("Rep or Base or Id is null");

				var url = Path.Combine(Base, Rep, Id); // + queryString;

				var reportInfo = await _reportService.GetReportInfoAsync(url, (prms) =>
				{
					// TODO:!!!! set Params
					prms.Set("UserId", _currentUser.Identity.Id);
					prms.Set("Id", Int64.Parse(Id));
				});

				var rep = _reportEngine.CreateReport(reportInfo);
				return await StiNetCoreViewer.GetReportResultAsync(this, rep);
			}
			catch (Exception ex)
			{
				String msg = ex.Message;
				Int32 x = msg.IndexOf(": error");
				if (x != -1)
					msg = msg[(x + 7)..].Trim();
				return StatusCode(500, msg);
			}
		}


		public Task<IActionResult> ViewerEvent()
		{
			return StiNetCoreViewer.ViewerEventResultAsync(this);
		}

		public Task<IActionResult> Interaction()
		{
			return StiNetCoreViewer.InteractionResultAsync(this);
		}

		public Task<IActionResult> PrintReport()
		{
			return StiNetCoreViewer.PrintReportResultAsync(this);
		}

		public Task<IActionResult> ExportReport()
		{
			return StiNetCoreViewer.ExportReportResultAsync(this);
		}
	}
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using A2v10.Infrastructure;

namespace A2v10.Core.Web.Mvc.Controllers
{
	[Route("report/[action]/{Id}")]
	[ExecutingFilter]
	[Authorize]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class ReportController : BaseController
	{
		private readonly IDataService _dataService;

		public ReportController(IApplicationHost host,
			ILocalizer localizer, IUserStateManager userStateManager, IProfiler profiler, IDataService dataService)
			: base(host, localizer, userStateManager, profiler)
		{
			_dataService = dataService;
		}

		[HttpGet]
		public Task Export(String Id)
		{
			throw new NotImplementedException(nameof(Export) + Id);
		}

		[HttpGet]
		public Task Print(String Id)
		{
			throw new NotImplementedException(nameof(Print) + Id);
		}

		// stimulsoft support
		public Task<IActionResult> GetReport()
		{
			throw new NotImplementedException(nameof(GetReport));
		}
	}
}

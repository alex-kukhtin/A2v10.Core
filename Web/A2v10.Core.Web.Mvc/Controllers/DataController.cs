using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace A2v10.Core.Web.Mvc.Controllers
{
	[Route("_data/[action]")]
	public class DataController : BaseController
	{
		public DataController(IDbContext dbContext, IApplicationHost host, IAppCodeProvider codeProvider,
			ILocalizer localizer, IUserStateManager userStateManager, IProfiler profiler)
			: base(dbContext, host, codeProvider, localizer, userStateManager, profiler)
		{
		}

		public IActionResult Reload()
		{
			return Content("RELOAD HERE");
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

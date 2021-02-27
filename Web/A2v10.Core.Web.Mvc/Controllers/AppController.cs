
using System;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2v10.Core.Web.Mvc.Controllers
{
	[Authorize]
	[Route("app/[action]")]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class AppController : Controller
	{
		public IActionResult About()
		{
			throw new NotImplementedException("AppController.About");
		}
	}
}

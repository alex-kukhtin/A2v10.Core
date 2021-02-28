
using System;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using A2v10.Web.Identity;

namespace A2v10.Core.Web.Mvc.Controllers
{
	[Authorize]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class MainController : Controller
	{
		[Route("{*pathInfo}")]
		public IActionResult Default(String pathInfo)
		{
			ViewBag.__Locale = "uk";
			ViewBag.__Build = 8000;
			ViewBag.__Minify = "min.";
			ViewBag.__Theme = "classic";
			ViewBag.__PersonName = User.Identity.GetUserPersonName();
			return View();
		}
	}
}

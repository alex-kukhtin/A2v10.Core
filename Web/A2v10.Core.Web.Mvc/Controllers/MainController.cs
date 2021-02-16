
using System;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Core.Web.Mvc.Controllers
{
	public class MainController : Controller
	{
		[Route("{*pathInfo}")]
		public IActionResult Default(String pathInfo)
		{
			ViewBag.__Locale = "uk";
			ViewBag.__Build = 8000;
			ViewBag.__Minify = "min.";
			ViewBag.__Theme = "classic";
			ViewBag.__PersonName = "Person name";
			return View();
		}
	}
}

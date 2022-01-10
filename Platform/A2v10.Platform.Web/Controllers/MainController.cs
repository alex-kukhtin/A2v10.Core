// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using A2v10.Infrastructure;
using A2v10.Web.Identity;
using System.Threading.Tasks;
using System.IO;

namespace A2v10.Platform.Web.Controllers
{
	[Authorize]
	[ExecutingFilter]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class MainController : Controller
	{
		private readonly IAppConfiguration _appConfiguration;
		private readonly IDataService _dataService;

		public MainController(IAppConfiguration appConfiguration, IDataService dataService)
		{
			_appConfiguration = appConfiguration;
			_dataService = dataService;
		}

		String? NormalizePathInfo(String? pathInfo)
        {
			if (String.IsNullOrEmpty(pathInfo))
				return null;
			var parts = pathInfo.Split(new Char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
			if (parts.Length == 1)
				return $"{pathInfo}{Path.DirectorySeparatorChar}index{Path.DirectorySeparatorChar}0";
			return pathInfo;
		}

		[Route("{*pathInfo}")]
		[HttpGet]
		public async Task<IActionResult> Default(String? pathInfo)
		{
			if (IsStaticFile())
				return NotFound();

			// TODO: modelStyles, ModelScripts
			var layoutDescr = await _dataService.GetLayoutDescriptionAsync(NormalizePathInfo(pathInfo));

			if (User.Identity == null)
				throw new ApplicationException("Invalid User");
			var viewModel = new MainViewModel()
			{
				PersonName = User.Identity.GetUserPersonName() ?? User.Identity.Name ?? throw new ApplicationException("Invalid UserName"),
				Debug = _appConfiguration.Debug,
				HelpUrl = "http://TODO/HELP_URL",
				ModelStyles = layoutDescr?.ModelStyles,
				ModelScripts = layoutDescr?.ModelScripts
			};
			ViewBag.__Minify = "min.";
			ViewBag.__Theme = "classic";


			if (pathInfo != null && pathInfo.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
				return View("Default.admin", viewModel);
			return View(viewModel);
		}

		public Boolean IsStaticFile()
		{
			var path = Request?.Path.ToString();
			if (path == null)
				return false;
			return path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
				path.EndsWith(".js", StringComparison.OrdinalIgnoreCase);
		}

	}
}

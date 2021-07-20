// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Platform.Web.Controllers
{
	[Authorize]
	[ExecutingFilter]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class MainController : Controller
	{
		private readonly IAppConfiguration _appConfiguration;

		public MainController(IAppConfiguration appConfiguration)
		{
			_appConfiguration = appConfiguration;
		}

		[Route("{*pathInfo}")]
		[HttpGet]
		public IActionResult Default(String pathInfo)
		{
			var viewModel = new MainViewModel()
			{
				PersonName = User.Identity.GetUserPersonName(),
				Debug = _appConfiguration.Debug,
				HelpUrl = "http://TODO/HELP_URL"
			};
			ViewBag.__Minify = "min.";
			ViewBag.__Theme = "classic";

			if (pathInfo != null && pathInfo.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
				return View("Default.admin", viewModel);
			return View(viewModel);
		}
	}
}

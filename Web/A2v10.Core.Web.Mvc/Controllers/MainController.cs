// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using A2v10.Web.Identity;
using A2v10.Infrastructure;

namespace A2v10.Core.Web.Mvc.Controllers
{
	[Authorize]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class MainController : Controller
	{
		private readonly IAppConfiguration _appConfiguration;

		public MainController(IAppConfiguration appConfiguration)
		{
			_appConfiguration = appConfiguration;
		}

		[Route("{*pathInfo}")]
		public IActionResult Default(String pathInfo)
		{
			var viewModel = new MainViewModel()
			{
				PersonName = User.Identity.GetUserPersonName(),
				Debug = _appConfiguration.Debug,
				HelpUrl = "http://TODO/HELP_URL"
			};
			ViewBag.__Locale = "uk";
			ViewBag.__Build = 8000; // TODO: Build Value
			ViewBag.__Minify = "min.";
			ViewBag.__Theme = "classic";
			return View(viewModel);
		}
	}
}

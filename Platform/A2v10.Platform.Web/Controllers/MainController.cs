// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System;

using System.Threading.Tasks;
using System.IO;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Platform.Web.Controllers;

[Authorize]
[ExecutingFilter]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class MainController : Controller
{
	private readonly AppOptions _appOptions;
	private readonly IDataService _dataService;
	private readonly IApplicationTheme _appTheme;
	private readonly IAppCodeProvider _codeProvider;

	public MainController(IDataService dataService, IOptions<AppOptions> appOptions, 
		IApplicationTheme appTheme, IAppCodeProvider codeProvider)
	{
		_appOptions = appOptions.Value;
		_dataService = dataService;
		_appTheme = appTheme;
		_codeProvider = codeProvider;
	}

	static String? NormalizePathInfo(String? pathInfo)
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

		var layoutDescr = await _dataService.GetLayoutDescriptionAsync(NormalizePathInfo(pathInfo));

		if (User.Identity == null)
			throw new ApplicationException("Invalid User");
		var viewModel = new MainViewModel()
		{
			PersonName = User.Identity.GetUserPersonName() ?? User.Identity.Name ?? throw new ApplicationException("Invalid UserName"),
			Debug = _appOptions.Environment.IsDebug,
			HelpUrl = "http://TODO/HELP_URL",
			ModelStyles = layoutDescr?.ModelStyles,
			ModelScripts = layoutDescr?.ModelScripts,
			HasNavPane = HasNavPane(),
			Theme = _appTheme.MakeTheme()
		};
		ViewBag.__Minify = "min.";

		if (pathInfo != null && pathInfo.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
			return View("Default.admin", viewModel);
		return View(viewModel);
	}

	private Boolean HasNavPane()
	{
		String path = _codeProvider.MakeFullPath("_navpane", "model.json", false);
		return _codeProvider.FileExists(path);
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

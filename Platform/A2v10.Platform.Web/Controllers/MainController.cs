// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System;

using System.Threading.Tasks;
using System.IO;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Diagnostics;

using A2v10.Infrastructure;
using A2v10.Web.Identity;
using A2v10.Module.Infrastructure;
using System.Linq;

namespace A2v10.Platform.Web.Controllers;

[Authorize]
[ExecutingFilter]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class MainController(IDataService dataService, IOptions<AppOptions> appOptions,
    IApplicationTheme appTheme, IAppCodeProvider codeProvider, ILicenseManager licenseManager,
    ICurrentUser currentUser) : Controller
{
	private readonly AppOptions _appOptions = appOptions.Value;
	private readonly IDataService _dataService = dataService;
	private readonly IApplicationTheme _appTheme = appTheme;
	private readonly IAppCodeProvider _codeProvider = codeProvider;
	private readonly ILicenseManager _licenseManager = licenseManager;
	private readonly ICurrentUser _currentUser = currentUser;

    static String? NormalizePathInfo(String? pathInfo)
        {
		if (String.IsNullOrEmpty(pathInfo))
			return null;
		var parts = pathInfo.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
		if (parts.Length == 1)
			return $"{pathInfo}{Path.DirectorySeparatorChar}index{Path.DirectorySeparatorChar}0";
		return pathInfo;
	}

	[Route("{*pathInfo}")]
	[HttpGet]
	public async Task<IActionResult> Default(String? pathInfo)
	{
		if (IsStaticFile() || (pathInfo != null && pathInfo.StartsWith('_')))
			return NotFound();

		if (!await CheckLicenseAsync())
			return new EmptyResult();	

		if (User.Identity == null)
			throw new ApplicationException("Invalid User");

		var layoutDescr = await _dataService.GetLayoutDescriptionAsync(NormalizePathInfo(pathInfo));

		var viewModel = new MainViewModel()
		{
			PersonName = User.Identity.GetUserPersonName() ?? User.Identity.Name ?? throw new ApplicationException("Invalid UserName"),
			Debug = _appOptions.Environment.IsDebug,
			HelpUrl = _appOptions.HelpUrl,
			ModelStyles = layoutDescr?.ModelStyles,
			ModelScripts = layoutDescr?.ModelScripts,
			HasNavPane = HasNavPane(),
			HasProfile = HasProfile(),
			Theme = _appTheme.MakeTheme(),
			HasSettings = _currentUser.Identity.IsAdmin && HasSettings(),
			Minify = _appOptions.Environment.IsRelease ? "min." : String.Empty,
		};

		if (pathInfo != null && _appOptions.SinglePages.Any(x => pathInfo.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
		{
			viewModel.SinglePagePath = pathInfo;
			return View("Default.singlepage", viewModel);
		}

		if (!String.IsNullOrEmpty(_appOptions.Layout))
			return View($"Default.{_appOptions.Layout}", viewModel);

		return View(viewModel);
	}

	[Route("main/error")]
	[HttpGet]
	[AllowAnonymous]
	public IActionResult Error(String? _1/*pathInfo*/)
	{
        //var RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        var exceptionHandlerPathFeature =
            HttpContext.Features.Get<IExceptionHandlerPathFeature>();

		String? ExceptionMessage = String.Empty;

        if (exceptionHandlerPathFeature?.Error is FileNotFoundException)
        {
            ExceptionMessage = "The file was not found.";
        }
		else if (exceptionHandlerPathFeature?.Error is Exception ex)
		{
			ExceptionMessage = ex.Message;
		}

        if (exceptionHandlerPathFeature?.Path == "/")
        {
            ExceptionMessage ??= string.Empty;
            ExceptionMessage += " Page: Home.";
        }
        return View("Error", ExceptionMessage);
	}

    private Boolean HasNavPane()
	{
		return _codeProvider.IsFileExists("_navpane/model.json");
	}
	private Boolean HasSettings()
	{
		return _codeProvider.IsFileExists("settings/model.json");
	}
	private Boolean HasProfile()
	{
		return _codeProvider.IsFileExists("_profile/model.json");
	}
	public Boolean IsStaticFile()
	{
		var path = Request?.Path.ToString();
		if (path == null)
			return false;
		return path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
			path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
			path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
			path.EndsWith(".map", StringComparison.OrdinalIgnoreCase);
	}

	private async Task<Boolean> CheckLicenseAsync()
	{
		if (!_codeProvider.HasLicensedModules)
			return true;
		return await _licenseManager.VerifyLicensesAsync(
			_currentUser.Identity.Segment, _currentUser.Identity.Tenant, 
			_codeProvider.LicensedModules);
	}
}

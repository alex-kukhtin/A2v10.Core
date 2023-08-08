// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Web.Identity;
using Microsoft.Extensions.Options;

namespace A2v10.Platform.Web.Controllers;

public record MultiTenantParamJson(String Companies, String Period);

[Route("_shell/[action]")]
[Authorize]
[ExecutingFilter]
public class ShellController : Controller
{
	private readonly IApplicationHost _host;
	private readonly IDbContext _dbContext;
	private readonly ICurrentUser _currentUser;
	private readonly IProfiler _profiler;
	private readonly IAppCodeProvider _codeProvider;
	private readonly IAppDataProvider _appDataProvider;
	private readonly AppOptions _appOptions;
	private readonly ILocalizer _localizer;

	const String MENU_PROC = "a2ui.[Menu.User.Load]";

	public ShellController(IDbContext dbContext, IApplicationHost host, ICurrentUser currentUser, IProfiler profiler,
		ILocalizer localizer, IAppCodeProvider codeProvider, IAppDataProvider appDataProvider, IOptions<AppOptions> appOptions)
	{
		_host = host;
		_dbContext = dbContext;
		_profiler = profiler;
		_codeProvider = codeProvider;
		_localizer = localizer;
		_currentUser = currentUser;
		_appDataProvider = appDataProvider;
		_appOptions = appOptions.Value;
	}

	Int64? UserId => User.Identity.GetUserId<Int64?>();
	Int32? TenantId => User.Identity.GetUserTenant<Int32>();

	public Boolean IsDebugConfiguration => _appOptions.Environment.IsDebug;

	public IActionResult Trace()
	{
		try
		{
			String json = _profiler.GetJson() ?? "{}";
			return new WebActionResult(json);
		}
		catch (Exception /*ex*/)
		{
			//WriteExceptionStatus(ex);
			throw;
		}
	}

	public Task<IActionResult> Script()
	{
		if (!String.IsNullOrEmpty(_appOptions.Layout))
			return DoScriptPlain();
		return DoScript();
	}

	async Task<IActionResult> DoScript()
	{
		try
		{
			Response.ContentType = MimeTypes.Application.Javascript;
			var script = await BuildScript();
			return new WebActionResult(script);
		}
		catch (Exception ex)
		{
			return new WebExceptionResult(500, ex.Message);
		}
	}

	async Task<IActionResult> DoScriptPlain()
	{
		try
		{
			Response.ContentType = MimeTypes.Application.Javascript;
			var script = await BuildScriptPlain();
			return new WebActionResult(script);
		}
		catch (Exception ex)
		{
			return new WebExceptionResult(500, ex.Message);
		}
	}

	public Task AppScripts()
	{
		Response.ContentType = MimeTypes.Application.Javascript;
		using var textWriter = new StreamWriter(Response.BodyWriter.AsStream());
		GetAppFiles("js", textWriter);
		return Task.CompletedTask;
	}

	[AllowAnonymous]
	public Task AppStyles()
	{
		Response.ContentType = MimeTypes.Text.Css;
		using var textWriter = new StreamWriter(Response.BodyWriter.AsStream());
		GetAppFiles("css", textWriter);
		return Task.CompletedTask;
	}

	void SetSqlParams(ExpandoObject prms)
	{
		if (UserId.HasValue)
			prms.Set("UserId", UserId.Value);
		if (_appOptions.MultiTenant && TenantId.HasValue)
			prms.Set("TenantId", TenantId);
	}


	async Task<String> BuildScriptPlain()
	{
		String shell = Resource.shellPlain;

		ExpandoObject loadPrms = new();
		SetSqlParams(loadPrms);

		ExpandoObject macros = new();

		_ = macros.Append(new Dictionary<String, Object?>
		{
			{ "AppVersion", _appDataProvider.AppVersion },
			{ "Debug", IsDebugConfiguration ? "true" : "false" },
			{ "AppData", await _appDataProvider.GetAppDataAsStringAsync() }
		});

		String proc = MENU_PROC;

		if (_appOptions.IsCustomUserMenu)
			proc = _appOptions.UserMenu!;
		IDataModel dm = await _dbContext.LoadModelAsync(_host.TenantDataSource, proc, loadPrms);

		ExpandoObject? menuRoot = dm.Root.RemoveEmptyArrays();
		SetUserStatePermission(dm);
		SetUserStateModules(dm);

		String jsonMenu = JsonConvert.SerializeObject(menuRoot, JsonHelpers.ConfigSerializerSettings(_host.IsDebugConfiguration));
		macros.Set("Menu", jsonMenu);

		return shell.ResolveMacros(macros) ?? String.Empty;
	}

	async Task<String> BuildScript()
	{
		String shell = Resource.shell;

		ExpandoObject loadPrms = new();
		SetSqlParams(loadPrms);

		ExpandoObject macros = new();

		_ = macros.Append(new Dictionary<String, Object?>
		{
			{ "AppVersion", _appDataProvider.AppVersion },
			{ "TenantAdmin", _currentUser.Identity.IsTenantAdmin ? "true" : "false" },
			{ "Admin", "false" },
			{ "Debug", IsDebugConfiguration ? "true" : "false" },
			{ "AppData", await _appDataProvider.GetAppDataAsStringAsync() },
			{ "Companies", "null" },
			{ "Period", "null" },
		});

		Boolean setCompany = false;
		/*
		if (_host.IsMultiTenant || _host.IsUsePeriodAndCompanies)
		{
			// for all users (include features)
			var res = await ProcessMultiTenantParams(loadPrms);
			if (res != null)
			{
				if (res.Companies != null)
					macros.Set("Companies", res.Companies);
				if (res.Period != null)
					macros.Set("Period", res.Period);
			}
		}
		else if (_host.IsMultiCompany && !bAdmin)
		{
			setCompany = true;
		}
		*/
		if (_host.Mobile)
			loadPrms.Set("Mobile", true);

		String proc = MENU_PROC;

		if (_appOptions.IsCustomUserMenu)
			proc = _appOptions.UserMenu!;

		IDataModel dm = await _dbContext.LoadModelAsync(_host.TenantDataSource, proc, loadPrms);

		ExpandoObject? menuRoot = dm.Root.RemoveEmptyArrays();
		SetUserStatePermission(dm);
		SetUserStateModules(dm);

		String jsonMenu = JsonConvert.SerializeObject(menuRoot, JsonHelpers.ConfigSerializerSettings(_host.IsDebugConfiguration));
		macros.Set("Menu", jsonMenu);

		if (setCompany)
		{
			var comps = dm.Root.Get<List<ExpandoObject>>("Companies");
			var currComp = (comps?.Find(c => c.Get<Boolean>("Current"))) 
				?? throw new InvalidDataException("There is no current company");
            var menuJson = JsonConvert.SerializeObject(comps);
			macros.Set("Companies", $"{{menu:{menuJson}, links:null}}");

			_currentUser.SetCompanyId(currComp.Get<Int64>("Id"));

		}

		return shell.ResolveMacros(macros) ?? String.Empty;
	}

	void SetUserStatePermission(IDataModel model)
	{
		_currentUser.SetReadOnly(model.Eval<Boolean>("UserState.ReadOnly"));
	}

    static void SetUserStateModules(IDataModel _1/*dm*/)
	{
	}

    void GetAppFiles(String ext, TextWriter writer)
	{
		var files = _codeProvider.EnumerateAllFiles("_assets", $"*.{ext}");
		if (files == null)
			return;
		foreach (var f in files)
		{
			var fileName = f.ToLowerInvariant();
			if (!fileName.EndsWith($".min.{ext}"))
			{
				String minFile = fileName.Replace($".{ext}", $".min.{ext}");
				if (_codeProvider.IsFileExists(minFile))
					continue; // min.{ext} found
			}
			using var stream = _codeProvider.FileStreamRO(fileName)
				?? throw new InvalidOperationException($"File not found '{fileName}'");
			using var sr = new StreamReader(stream);
			var txt = sr.ReadToEnd();
			if (txt.StartsWith("/*!@localize*/"))
				txt = _localizer.Localize(null, txt, false);
			writer.Write(txt);
		}
	}
}


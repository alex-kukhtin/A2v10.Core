// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers;

public class PageActionResult : IActionResult
{
	private readonly IRenderResult _render;
	private readonly String? _script;

	public PageActionResult(IRenderResult render, String? script)
	{
		_render = render;
		_script = script;
	}

	public async Task ExecuteResultAsync(ActionContext context)
	{
		var resp = context.HttpContext.Response;
		resp.ContentType = _render.ContentType;
		await resp.WriteAsync(_render.Body, Encoding.UTF8);
		if (_script != null)
			await resp.WriteAsync(_script, Encoding.UTF8);
	}
}

[ExecutingFilter]
[Authorize]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class PageController : BaseController
{
	private readonly IDataService _dataService;
	private readonly IDataScripter _scripter;
	private readonly IAppCodeProvider _codeProvider;
	private readonly IViewEngineProvider _viewEngineProvider;
	private readonly IAppDataProvider _appDataProvider;

	public PageController(IApplicationHost host, IAppCodeProvider codeProvider,
		ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler, IDataService dataService, 
		IViewEngineProvider viewEngineProvider, IAppDataProvider appDataProvider)
		: base(host, localizer, currentUser, profiler)
	{
		_dataService = dataService;
		_codeProvider = codeProvider;
		_scripter = new VueDataScripter(host, codeProvider, _localizer, currentUser);
		_viewEngineProvider = viewEngineProvider;
		_appDataProvider = appDataProvider;
	}

	[Route("_page/{*pathInfo}")]
	[Route("admin/_page/{*pathInfo}")]
	public async Task<IActionResult> Page(String pathInfo)
	{
		// {pagePath}/action/id
		if (pathInfo.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
			return await RenderApplicationPage(UrlKind.Page, pathInfo);
		return await Render(pathInfo + Request.QueryString, UrlKind.Page);
	}

	[Route("_dialog/{*pathInfo}")]
	[Route("admin/_dialog/{*pathInfo}")]
	public async Task<IActionResult> Dialog(String pathInfo)
	{
		// {pagePath}/dialog/id
		if (pathInfo.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
			return await RenderApplicationPage(UrlKind.Dialog, pathInfo);
		return await Render(pathInfo + Request.QueryString, UrlKind.Dialog);
	}

	[Route("_popup/{*pathInfo}")]
	[Route("admin/_popup/{*pathInfo}")]
	public Task<IActionResult> Popup(String pathInfo)
	{
		// {pagePath}/popup/id
		return Render(pathInfo + Request.QueryString, UrlKind.Popup);
	}

	async Task<IActionResult> Render(String path, UrlKind kind)
	{
		try
		{
			var modelAndView = await _dataService.LoadAsync(kind, path, SetSqlQueryParams);
			return await Render(modelAndView);
		} 
		catch (Exception ex)
		{
			if (ex.Message.StartsWith("UI:"))
				return WriteExceptionStatus(ex);
			else
				return WriteHtmlException(ex);
		}
	}

	async Task<IActionResult> Render(IDataLoadResult modelAndView, Boolean secondPhase = false)
	{
		Response.ContentType = MimeTypes.Text.HtmlUtf8;

		IDataModel? model = modelAndView.Model;
		var rw = modelAndView.View;

		String rootId = $"el{Guid.NewGuid()}";

		//var typeChecker = _host.CheckTypes(rw.Path, rw.checkTypes, model);

		var msi = new ModelScriptInfo()
		{
			DataModel = model,
			RootId = rootId,
			IsDialog = rw.IsDialog,
			IsIndex = rw.IsIndex,
			IsSkipDataStack = rw.IsSkipDataStack,
			Template = rw.Template,
			Path = rw.Path,
			BaseUrl = rw.BaseUrl
		};

		var si = await _scripter.GetModelScript(msi);

		var viewName = _codeProvider.MakeFullPath(rw.Path, rw.GetView(_host.Mobile), _currentUser.IsAdminApplication);
		var viewEngine = _viewEngineProvider.FindViewEngine(viewName);

		// render XAML
		var ri = new RenderInfo()
		{
			RootId = rootId,
			FileName = viewEngine.FileName,
			FileTitle = rw.GetView(_host.Mobile),
			Path = rw.BaseUrl,
			DataModel = model,
			//TypeChecker = typeChecker,
			CurrentLocale = null,
			IsDebugConfiguration = _host.IsDebugConfiguration,
			SecondPhase = secondPhase,
			Admin = _currentUser.IsAdminApplication
		};

		var result = await viewEngine.Engine.RenderAsync(ri);

		await ProcessDbEvents(rw);
		return new PageActionResult(result, si.Script);
	}

	async Task<IActionResult> RenderApplicationPage(UrlKind urlKind, String pathInfo)
	{
		String exceptionInfo = $"Invald application url: '{pathInfo}'";
		if (pathInfo == null)
			throw new InvalidReqestExecption(exceptionInfo);
		var info = pathInfo.Split(new Char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
		if (info.Length < 2)
			throw new InvalidReqestExecption(exceptionInfo);
		var kind = info[1].ToLowerInvariant();
		switch (kind)
		{
			case "about":
				return View("About", new AboutViewModel() { AppData = await _appDataProvider.GetAppDataAsStringAsync()});
			case "changepassword":
				if (urlKind != UrlKind.Dialog)
					throw new InvalidReqestExecption(exceptionInfo);
				return View("ChangePassword");
			default:
				if (urlKind != UrlKind.Page)
					throw new InvalidReqestExecption(exceptionInfo);
				//var m = new AppPageModel();
				ViewBag.PageKind = kind;
				return View("Default" /*m*/);
		}
	}
}

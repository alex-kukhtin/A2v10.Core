// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using System.Dynamic;

namespace A2v10.Platform.Web.Controllers;

public class PageActionResult(IRenderResult render, String? script) : IActionResult
{
	private readonly IRenderResult _render = render;
	private readonly String? _script = script;

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
public class PageController(IApplicationHost _host, ILocalizer _localizer, ICurrentUser _currentUser, 
	IProfiler _profiler, IDataService _dataService,
	IViewEngineProvider _viewEngineProvider, IAppDataProvider _appDataProvider, 
	IAppVersion _appVersion, IDataScripter _scripter) 
	: BaseController(_host, _localizer, _currentUser, _profiler)
{
	[Route("_page/{*pathInfo}")]
	public async Task<IActionResult> Page(String pathInfo)
	{
		// {pagePath}/action/id
		if (pathInfo.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
			return await RenderApplicationPage(UrlKind.Page, pathInfo);
		return await Render(pathInfo + Request.QueryString, UrlKind.Page);
	}

    [Route("_export/{*pathInfo}")]
    public async Task<IActionResult> Export(String pathInfo)
    {
        // {pagePath}/action/id
        var res = await _dataService.ExportAsync(pathInfo, SetSqlQueryParams);

		var result = new WebBinaryActionResult(res.Body, res.ContentType);
		Response.ContentType = res.ContentType;

		var cdh = new ContentDispositionHeaderValue("attachment")
		{
			FileNameStar = Localize(res.FileName)
        };
        Response.Headers.Append("Content-Disposition", cdh.ToString());
        return result;
    }

    [Route("_dialog/{*pathInfo}")]
	public async Task<IActionResult> Dialog(String pathInfo)
	{
		// {pagePath}/dialog/id
		if (pathInfo.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
			return await RenderApplicationPage(UrlKind.Dialog, pathInfo);
		return await Render(pathInfo + Request.QueryString, UrlKind.Dialog);
	}

	[Route("_popup/{*pathInfo}")]
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
		if (modelAndView.ActionResult != null)
			return Content(modelAndView.ActionResult, MimeTypes.Text.HtmlUtf8);

		Response.ContentType = MimeTypes.Text.HtmlUtf8;
		Response.Headers.Append("App-Version", _appVersion.AppVersion);
		if (!String.IsNullOrEmpty(_appVersion.ModuleVersion))
			Response.Headers.Append("Module-Version", _appVersion.ModuleVersion);

		IDataModel? model = modelAndView.Model;
		var rw = modelAndView.View ?? 
			throw new InvalidOperationException("IModelView is null");

		String rootId = $"el{Guid.NewGuid()}";

		//var typeChecker = _host.CheckTypes(rw.Path, rw.checkTypes, model);

		var viewName = rw.GetView(_host.Mobile);
		var templateName = rw.Template;
		String? viewText = null;
		String? templateText = null;

		if (viewName == "@Model.View" || templateName == "@Model.Template")
		{
			var modelModel = model?.Eval<ExpandoObject>("Model")
				?? throw new InvalidOperationException("Model element not found");
			Boolean removeView = false;
			Boolean removeTemplate = false;
			if (viewName == "@Model.View") {
				viewText = modelModel.Get<String>("View") ??
					throw new InvalidOperationException("Model.View not found");
				modelModel.RemoveKeys("View");
				removeView = true;
			}
			if (templateName == "@Model.Template")
			{
				templateText = modelModel.Get<String>("Template") ??
					throw new InvalidOperationException("Model.Template not found");
				modelModel.RemoveKeys("Template");
				removeTemplate = true;
			}
			if (removeView && removeTemplate)
			{
				model.Metadata.Remove("TModel");
				model.Metadata["TRoot"].Fields.Remove("Model");
				model.Root.RemoveKeys("Model");
			}
		}

		var msi = new ModelScriptInfo()
		{
			DataModel = model,
			RootId = rootId,
			IsDialog = rw.IsDialog,
			IsIndex = rw.IsIndex,
			IsPlain = rw.IsPlain,
			IsSkipDataStack = rw.IsSkipDataStack,
			Template = templateText ?? rw.Template,
			Path = templateText != null ? "@Model.Template" : rw.Path,
			BaseUrl = rw.BaseUrl
		};

		var si = await _scripter.GetModelScript(msi);

		var viewEngine = _viewEngineProvider.FindViewEngine(rw.Path, viewName);

		// render XAML
		var ri = new RenderInfo()
		{
			RootId = rootId,
			FileName = (viewText == null) ? viewEngine.FileName : null,
			FileTitle = rw.GetView(_host.Mobile),
			Path = rw.Path,
			DataModel = model,
			Text = viewText,
			//TypeChecker = typeChecker,
			CurrentLocale = null,
			IsDebugConfiguration = _host.IsDebugConfiguration,
			SecondPhase = secondPhase,
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
		var info = pathInfo.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
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
				return View("ChangePassword", new ChangePasswordViewModel { UserName = _currentUser.Identity.Name});
			default:
				if (urlKind != UrlKind.Page)
					throw new InvalidReqestExecption(exceptionInfo);
				//var m = new AppPageModel();
				ViewBag.PageKind = kind;
				return View("Default" /*m*/);
		}
	}
}

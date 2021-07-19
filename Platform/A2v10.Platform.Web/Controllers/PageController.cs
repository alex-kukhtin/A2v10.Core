// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers
{
	[ExecutingFilter]
	[Authorize]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class PageController : BaseController
	{
		private readonly IDataService _dataService;
		private readonly IDataScripter _scripter;
		private readonly IAppCodeProvider _codeProvider;
		private readonly IViewEngineProvider _viewEngineProvider;

		public PageController(IApplicationHost host, IAppCodeProvider codeProvider,
			ILocalizer localizer, ICurrentUser currentUser, IUserStateManager userStateManager, IProfiler profiler,
			IDataService dataService, IViewEngineProvider viewEngineProvider, IUserLocale userLocale)
			: base(host, localizer, currentUser, userStateManager, profiler, userLocale)
		{
			_dataService = dataService;
			_codeProvider = codeProvider;
			_scripter = new VueDataScripter(host, codeProvider, _localizer, currentUser);
			_viewEngineProvider = viewEngineProvider;
		}

		[Route("_page/{*pathInfo}")]
		[Route("admin/_page/{*pathInfo}")]
		public async Task<IActionResult> Page(String pathInfo)
		{
			// {pagePath}/action/id
			if (pathInfo.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
				return RenderApplicationPage(UrlKind.Page, pathInfo);
			await Render(pathInfo + Request.QueryString, UrlKind.Page);
			return new EmptyResult();
		}

		[Route("_dialog/{*pathInfo}")]
		[Route("admin/_dialog/{*pathInfo}")]
		public async Task<IActionResult> Dialog(String pathInfo)
		{
			// {pagePath}/dialog/id
			if (pathInfo.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
				return RenderApplicationPage(UrlKind.Dialog, pathInfo);
			await Render(pathInfo + Request.QueryString, UrlKind.Dialog);
			return new EmptyResult();
		}

		[Route("_popup/{*pathInfo}")]
		[Route("admin/_popup/{*pathInfo}")]
		public Task Popup(String pathInfo)
		{
			// {pagePath}/popup/id
			return Render(pathInfo + Request.QueryString, UrlKind.Popup);
		}

		async Task Render(String path, UrlKind kind)
		{
			try
			{
				var modelAndView = await _dataService.LoadAsync(kind, path, SetSqlQueryParams);
				await Render(modelAndView);
			} 
			catch (Exception ex)
			{
				if (ex.Message.StartsWith("UI:"))
					await WriteExceptionStatus(ex);
				else
					await WriteHtmlException(ex);
			}
		}

		async Task Render(IDataLoadResult modelAndView, Boolean secondPhase = false)
		{
			Response.ContentType = MimeTypes.Text.HtmlUtf8;

			IDataModel model = modelAndView.Model;
			var rw = modelAndView.View;

			String rootId = $"el{Guid.NewGuid()}";

			//var typeChecker = _host.CheckTypes(rw.Path, rw.checkTypes, model);

			var msi = new ModelScriptInfo()
			{
				DataModel = model,
				RootId = rootId,
				IsDialog = rw.IsDialog,
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

			Response.ContentType = result.ContentType;
			await HttpResponseWritingExtensions.WriteAsync(Response, result.Body, Encoding.UTF8);
			await ProcessDbEvents(rw);
			await HttpResponseWritingExtensions.WriteAsync(Response, si.Script, Encoding.UTF8);
		}

		IActionResult RenderApplicationPage(UrlKind urlKind, String pathInfo)
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
					return View("About");
				case "changepassword":
					if (urlKind != UrlKind.Dialog)
						throw new InvalidReqestExecption(exceptionInfo);
					throw new Exception("ChangePassword");
				default:
					if (urlKind != UrlKind.Page)
						throw new InvalidReqestExecption(exceptionInfo);
					//var m = new AppPageModel();
					ViewBag.PageKind = kind;
					return View("Default" /*m*/);
			}
		}
	}
}

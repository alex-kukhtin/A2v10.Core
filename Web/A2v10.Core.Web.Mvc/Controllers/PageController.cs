// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Core.Web.Mvc.Builders;
using A2v10.System.Xaml;
using A2v10.Xaml;

namespace A2v10.Core.Web.Mvc.Controllers
{
	[ExecutingFilter]
	[Authorize]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public class PageController : BaseController
	{

		//public Boolean Admin => _host.IsAdminMode;

		private readonly IXamlReaderService _xamlReader;
		private readonly IDataService _dataService;
		private readonly IRenderer _renderer;
		private readonly IDataScripter _scripter;
		private readonly IAppCodeProvider _codeProvider;

		public PageController(IApplicationHost host, IAppCodeProvider codeProvider,
			ILocalizer localizer, IUserStateManager userStateManager, IProfiler profiler, IXamlReaderService xamlReader,
			IDataService dataService)
			: base(host, localizer, userStateManager, profiler)
		{
			_xamlReader = xamlReader;
			_dataService = dataService;
			_codeProvider = codeProvider;
			_renderer = new XamlRenderer(_profiler, codeProvider, xamlReader);
			_scripter = new VueDataScripter(host, codeProvider, _localizer);
		}

		[Route("_page/{*pathInfo}")]
		public Task Page(String pathInfo)
		{
			// {pagePath}/action/id
			return Render(pathInfo, UrlKind.Page);
		}

		[Route("_dialog/{*pathInfo}")]
		public Task Dialog(String pathInfo)
		{
			// {pagePath}/dialog/id
			return Render(pathInfo, UrlKind.Dialog);
		}

		[Route("_popup/{*pathInfo}")]
		public Task Popup(String pathInfo)
		{
			// {pagePath}/popup/id
			return Render(pathInfo, UrlKind.Popup);
		}


		async Task Render(String path, UrlKind kind)
		{
			try
			{
				var modelAndView = await _dataService.Load(kind, path, SetSqlQueryParams);

				await Render(modelAndView);
			} 
			catch (Exception ex)
			{
				if (ex.Message.StartsWith("UI:"))
				{
					var error = Localize(ex.Message[3..]);
					WriteExceptionStatus(ex);
				}
				else
				{
					await WriteHtmlException(ex);
				}
			}
		}

		async Task Render(IDataLoadResult modelAndView, Boolean secondPhase = false)
		{
			Response.ContentType = MimeTypes.Text.HtmlUtf8;

			IDataModel model = modelAndView.Model;
			var rw = modelAndView.View;

			String rootId = "el" + Guid.NewGuid().ToString();

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

			String modelScript = si.Script;

			// try xaml
			String fileName = rw.GetView(_host.Mobile) + ".xaml";
			String basePath = rw.BaseUrl;

			String filePath = _codeProvider.MakeFullPath(rw.Path, fileName);

			Boolean bRendered = false;
			if (_codeProvider.FileExists(filePath))
			{
				// render XAML
				using var strWriter = new StringWriter();
				var ri = new RenderInfo()
				{
					RootId = rootId,
					FileName = filePath,
					FileTitle = fileName,
					Path = basePath,
					Writer = strWriter,
					DataModel = model,
					Localizer = _localizer,
					//TypeChecker = typeChecker,
					CurrentLocale = null,
					IsDebugConfiguration = _host.IsDebugConfiguration,
					SecondPhase = secondPhase
				};
				_renderer.Render(ri);
				// write markup
				await HttpResponseWritingExtensions.WriteAsync(Response, strWriter.ToString(), Encoding.UTF8);
				bRendered = true;
			}
			else
			{
				// try html
				fileName = rw.GetView(_host.Mobile) + ".html";
				filePath = _codeProvider.MakeFullPath(rw.Path, fileName);
				if (_codeProvider.FileExists(filePath))
				{
					using (_profiler.CurrentRequest.Start(ProfileAction.Render, $"render: {fileName}"))
					{
						using var tr = new StreamReader(filePath);
						String htmlText = await tr.ReadToEndAsync();
						htmlText = htmlText.Replace("$(RootId)", rootId);
						htmlText = _localizer.Localize(null, htmlText, false);
						await HttpResponseWritingExtensions.WriteAsync(Response, htmlText, Encoding.UTF8);
						bRendered = true;
					}
				}
			}
			if (!bRendered)
			{
				//throw new RequestModelException($"The view '{rw.GetView(_host.Mobile)}' was not found. The following locations were searched:\n{rw.GetRelativePath(".xaml", _host.Mobile)}\n{rw.GetRelativePath(".html", _host.Mobile)}");
				// TODO:
				throw new RequestModelException($"The view '{rw.GetView(_host.Mobile)}' was not found. The following locations were searched:");
				//\n{rw.GetRelativePath(".xaml", _host.Mobile)}\n{rw.GetRelativePath(".html", _host.Mobile)}");
			}
			//await ProcessDbEvents(rw);
			await HttpResponseWritingExtensions.WriteAsync(Response, modelScript, Encoding.UTF8);
		}
	}
}

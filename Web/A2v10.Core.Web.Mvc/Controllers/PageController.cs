// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;
using System.Web;

using Microsoft.AspNetCore.Mvc;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Core.Web.Mvc.Builders;
using A2v10.System.Xaml;

namespace A2v10.Core.Web.Mvc.Controllers
{
	public class PageController : BaseController
	{

		public Boolean Admin => _host.IsAdminMode;

		private readonly IXamlReaderService _xamlReader;
		public PageController(IDbContext dbContext, IApplicationHost host, IAppCodeProvider codeProvider,
			ILocalizer localizer, IUserStateManager userStateManager, IProfiler profiler, IXamlReaderService xamlReader)
			: base(dbContext, host, codeProvider, localizer, userStateManager, profiler)
		{
			_xamlReader = xamlReader;
		}

		[Route("_page/{*pathInfo}")]
		public Task Page(String pathInfo)
		{
			// {pagePath}/action/id
			return Render(pathInfo, RequestUrlKind.Page);
		}

		[Route("_dialog/{*pathInfo}")]
		public Task Dialog(String pathInfo)
		{
			// {pagePath}/dialog/id
			return Render(pathInfo, RequestUrlKind.Dialog);
		}

		[Route("_popup/{*pathInfo}")]
		public Task Popup(String pathInfo)
		{
			// {pagePath}/popup/id
			return Render(pathInfo, RequestUrlKind.Popup);
		}

		async Task Render(String path, RequestUrlKind kind)
		{
			try
			{
				ExpandoObject loadPrms = new ExpandoObject();
				path = path.ToLowerInvariant();
				loadPrms.Append(CheckPeriod(HttpUtility.ParseQueryString(Request.QueryString.Value)), toPascalCase: true);
				SetSqlQueryParams(loadPrms);

				RequestModel rm = await RequestModel.CreateFromUrl(_codeProvider, kind, path);
				RequestView rw = rm.GetCurrentAction(kind);
				var pageBuilder = new PageBuilder(_host, _dbContext, _codeProvider, _localizer, _userStateManager, _profiler, _xamlReader);
				await pageBuilder.Render(rw, loadPrms, Response);
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
	}
}

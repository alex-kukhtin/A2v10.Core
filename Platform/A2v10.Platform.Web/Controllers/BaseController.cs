// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Net;
using System.Text;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Platform.Web
{

	public class BaseController : Controller, IControllerProfiler, IControllerTenant, IControllerAdmin, IControllerLocale
	{
		protected readonly IApplicationHost _host;
		protected readonly ILocalizer _localizer;
		protected readonly IUserStateManager _userStateManager;
		protected readonly IProfiler _profiler;
		protected readonly IUserLocale _userLocale;

		public BaseController(IApplicationHost host, ILocalizer localizer, IUserStateManager userStateManager, IProfiler profiler, IUserLocale userLocale)
		{
			_host = host ?? throw new ArgumentNullException(nameof(host));
			_localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
			_userStateManager = userStateManager ?? throw new ArgumentNullException(nameof(userStateManager));
			_profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
			_userLocale = userLocale ?? throw new ArgumentNullException(nameof(userLocale));
			_profiler.Enabled = _host.IsDebugConfiguration;
		}

		protected Int64 UserId => User.Identity.GetUserId<Int64>();
		protected Int32 TenantId => User.Identity.GetUserTenantId();
		protected String UserSegement => User.Identity.GetUserSegment();
		Int64 CompanyId => _userStateManager.UserCompanyId(TenantId, UserId); 

		protected void SetSqlQueryParamsWithoutCompany(ExpandoObject prms)
		{
			SetUserTenantToParams(prms);
		}

		protected void SetSqlQueryParams(ExpandoObject prms)
		{
			SetUserTenantToParams(prms);
			SetUserCompanyToParams(prms);
		}

		void SetUserCompanyToParams(ExpandoObject prms)
		{
			if (_host.IsMultiCompany)
				prms.Set("CompanyId", CompanyId);
		}

		void SetUserTenantToParams(ExpandoObject prms)
		{
			prms.Set("UserId", UserId);
			if (_host.IsMultiTenant)
				prms.Set("TenantId", TenantId);
		}

		public void ProfileException(Exception ex)
		{
			using var _ = _profiler.CurrentRequest.Start(ProfileAction.Exception, ex.Message);
		}

		protected String Localize(String content)
		{
			return _localizer.Localize(null, content);
		}

		public async Task WriteHtmlException(Exception ex)
		{
			if (ex.InnerException != null)
				ex = ex.InnerException;
			ProfileException(ex);
			var msg = WebUtility.HtmlEncode(ex.Message);
			var stackTrace = WebUtility.HtmlEncode(ex.StackTrace);
			if (_host.IsDebugConfiguration)
			{
				var text = $"<div class=\"app-exception\"><div class=\"message\">{msg}</div><div class=\"stack-trace\">{stackTrace}</div></div>";
				await HttpResponseWritingExtensions.WriteAsync(Response, text, Encoding.UTF8);
			}
			else
			{
				msg = Localize("@[Error.Exception]");
				var link = Localize("@[Error.Link]");
				var text = $"<div class=\"app-exception\"><div class=message>{msg}</div><a class=link href=\"/\">{@link}</a></div>";
				await HttpResponseWritingExtensions.WriteAsync(Response, text, Encoding.UTF8);
			}
		}

		public Task WriteExceptionStatus(Exception ex, Int32 errorCode = 0)
		{
			if (ex.InnerException != null)
				ex = ex.InnerException;
			
			if (errorCode == 0)
				errorCode = 255;
			
			ProfileException(ex);

			//TODO::Response.SuppressContent = false;
			Response.StatusCode = errorCode; // CUSTOM ERROR!!!!
			Response.ContentType = MimeTypes.Text.Plain;
			//TODO::Response.StatusDescription = "Server error";
			return HttpResponseWritingExtensions.WriteAsync(Response, _localizer.Localize(null, ex.Message), Encoding.UTF8);
		}

		#region IControllerProfiler

		public IProfiler Profiler => _profiler;

		public IProfileRequest BeginRequest()
		{
			return _profiler.BeginRequest(Request.Path + Request.QueryString, null);
		}

		public void EndRequest(IProfileRequest request)
		{
			_profiler.EndRequest(request);
		}
		#endregion

		#region IControllerTenant

		public void StartTenant()
		{
			_host.TenantId = TenantId;
			_host.UserId = UserId;
			_host.UserSegment = UserSegement;
		}
		#endregion

		#region IControllerAdmin
		public void SetAdmin()
		{
			_userStateManager.SetAdmin();
		}
		#endregion

		#region IControllerLocale 
		public void SetLocale()
		{
			_userLocale.Locale = User.Identity.GetUserLocale();
		}
		#endregion

		public Task ProcessDbEvents(IModelView view)
		{
			return Task.CompletedTask;
		}
	}
}

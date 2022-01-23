// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Net;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers;
public class BaseController : Controller, IControllerProfiler
{
	protected readonly IApplicationHost _host;
	protected readonly ILocalizer _localizer;
	protected readonly IProfiler _profiler;
	protected readonly ICurrentUser _currentUser;

	public BaseController(IApplicationHost host, ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler)
	{
		_host = host ?? throw new ArgumentNullException(nameof(host));
		_localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
		_profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
		_profiler.Enabled = _host.IsDebugConfiguration;
		_currentUser = currentUser;
	}

	protected Int64 UserId => _currentUser?.Identity?.Id ?? throw new InvalidProgramException("UserId is null");
	protected Int32? TenantId => _currentUser.Identity.Tenant;
	protected Int64? CompanyId => _currentUser.State.Company;

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

	protected String? Localize(String? content)
	{
		return _localizer.Localize(null, content);
	}

	public IActionResult WriteHtmlException(Exception ex)
	{
		if (ex.InnerException != null)
			ex = ex.InnerException;
		ProfileException(ex);
		var msg = WebUtility.HtmlEncode(ex.Message);
		var stackTrace = WebUtility.HtmlEncode(ex.StackTrace);
		if (_host.IsDebugConfiguration)
		{
			var text = $"<div class=\"app-exception\"><div class=\"message\">{msg}</div><div class=\"stack-trace\">{stackTrace}</div></div>";
			return new WebActionResult(text, MimeTypes.Text.HtmlUtf8);
		}
		else
		{
			msg = Localize("@[Error.Exception]");
			var link = Localize("@[Error.Link]");
			var text = $"<div class=\"app-exception\"><div class=message>{msg}</div><a class=link href=\"/\">{@link}</a></div>";
			return new WebActionResult(text, MimeTypes.Text.HtmlUtf8);
		}
	}

	public IActionResult WriteExceptionStatus(Exception ex, Int32 errorCode = 0)
	{
		if (ex.InnerException != null)
			ex = ex.InnerException;
			
		if (errorCode == 0)
			errorCode = 255;
			
		ProfileException(ex);

		return new WebExceptionResult(errorCode, _localizer.Localize(null, ex.Message));
	}

	#region IControllerProfiler

	public IProfiler Profiler => _profiler;

	public IProfileRequest? BeginRequest()
	{
		return _profiler.BeginRequest(Request.Path + Request.QueryString, null);
	}

	public void EndRequest(IProfileRequest? request)
	{
		_profiler.EndRequest(request);
	}
	#endregion

	public static Task ProcessDbEvents(IModelView _)
	{
		return Task.CompletedTask;
	}

	public static IActionResult WriteImageException(Exception ex)
	{
		if (ex.InnerException != null)
			ex = ex.InnerException;

		var len = ex.Message.Length * 5;
		var svg =
		$@"<svg width='{len}px' height='40px' xmlns='http://www.w3.org/2000/svg'>
			<rect width='{len}' height='40' fill='#fff0f5' stroke='#880000' stroke-width='1'/>
			<text x='{len / 2}' y='25' fill='#880000' font-size='11px' text-anchor='middle'>{ex.Message}</text>
		</svg>";
		var res = new WebActionResult(svg, MimeTypes.Image.Svg);
		res.AddHeader("cache-control", "no-store,no-cache");
		return res;
	}
}


// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Core.Web.Mvc
{

	public class DataModelAndView
	{
		public IDataModel Model;
		public RequestView RequestView;
	}

	public class BaseController : Controller
	{
		protected readonly IApplicationHost _host;
		protected readonly IAppCodeProvider _codeProvider;
		protected readonly IDbContext _dbContext;
		protected readonly ILocalizer _localizer;
		protected readonly IUserStateManager _userStateManager;

		public BaseController(IDbContext dbContext, IApplicationHost host, IAppCodeProvider codeProvider,
			ILocalizer localizer, IUserStateManager userStateManager)
		{
			_dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
			_host = host ?? throw new ArgumentNullException(nameof(host));
			_codeProvider = codeProvider ?? throw new ArgumentNullException(nameof(codeProvider));
			_localizer = _localizer ?? throw new ArgumentNullException(nameof(localizer));
			_userStateManager = userStateManager ?? throw new ArgumentNullException(nameof(userStateManager));
		}

		Int64 UserId => 99; //TODO:
		Int32 TenantId => 0; // TODO:
		Int64 CompanyId => 1; // TODO: _baseController.UserStateManager.UserCompanyId(TenantId, UserId);

		protected void SetSqlQueryParamsWithoutCompany(ExpandoObject prms)
		{
			SetUserTenantToParams(prms);
			SetClaimsToParams(prms);
		}

		protected void SetSqlQueryParams(ExpandoObject prms)
		{
			SetUserTenantToParams(prms);
			SetUserCompanyToParams(prms);
			SetClaimsToParams(prms);
		}

		void SetUserCompanyToParams(ExpandoObject prms)
		{
			if (_host.IsMultiCompany)
				prms.Set("CompanyId", CompanyId);
		}

		protected void SetClaimsToParams(ExpandoObject prms)
		{
			//TODO::
			/*
			if (_baseController.Admin)
				return; // no claims for admin application
			String claims = _host.UseClaims;
			if (String.IsNullOrEmpty(claims))
				return;
			foreach (var s in claims.Split(','))
			{
				var strClaim = s.Trim().ToLowerInvariant();
				prms.Set(strClaim.ToPascalCase(), User.Identity.GetUserClaim(strClaim));
			}
			*/
		}

		void SetUserTenantToParams(ExpandoObject prms)
		{
			prms.Set("UserId", UserId);
			if (_host.IsMultiTenant)
			{
				prms.Set("TenantId", TenantId);
			}
		}

		public static NameValueCollection CheckPeriod(NameValueCollection coll)
		{
			var res = new NameValueCollection();
			foreach (var key in coll.Keys)
			{
				var k = key?.ToString();
				if (k.ToLowerInvariant() == "period")
				{
					// parse period
					var ps = coll[k].Split('-');
					res.Remove("From"); // replace prev value
					res.Remove("To");
					if (ps[0].ToLowerInvariant() == "all")
					{
						// from js! utils.date.minDate/maxDate
						res.Add("From", "19010101");
						res.Add("To", "29991231");
					}
					else
					{
						res.Add("From", ps[0]);
						res.Add("To", ps.Length == 2 ? ps[1] : ps[0]);
					}
				}
				else
				{
					res.Add(k, coll[k]);
				}
			}
			return res;
		}

		public void ProfileException(Exception ex)
		{
			/*
			TODO:
			using (Host.Profiler.CurrentRequest.Start(ProfileAction.Exception, ex.Message))
			{
				// do nothing
			}
			*/
		}

		protected String Localize(String msg)
		{
			// TODO:
			return msg;
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

		public void WriteExceptionStatus(Exception ex, Int32 errorCode = 0)
		{
			if (ex.InnerException != null)
				ex = ex.InnerException;
			if (errorCode == 0)
				errorCode = 255;
			ProfileException(ex);
			//TODO::Response.SuppressContent = false;
			Response.StatusCode = errorCode; // CUSTOM ERROR!!!!
			Response.ContentType = "text/plain";
			//TODO::Response.StatusDescription = "Server error";
			//TODO::Response.Write(Localize(ex.Message));
		}

	}
}

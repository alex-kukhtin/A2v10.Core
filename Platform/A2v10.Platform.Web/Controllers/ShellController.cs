// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Platform.Web
{

	public class MultiTenantParamJson
	{
		public String Companies { get; set; }
		public String Period { get; set; }
	}

	[Route("_shell/[action]")]
	[Authorize]
	[ExecutingFilter]
	public class ShellController : Controller, IControllerLocale
	{
		private readonly IApplicationHost _host;
		private readonly IDbContext _dbContext;
		private readonly IUserStateManager _userStateManager;
		private readonly IProfiler _profiler;
		private readonly IAppCodeProvider _codeProvider;
		private readonly ILocalizer _localizer;
		private readonly IUserLocale _userLocale;
		private readonly IAppVersion _appVersion;

		public ShellController(IDbContext dbContext, IApplicationHost host, IUserStateManager userStateManager, IProfiler profiler,
			IAppCodeProvider codeProvider, ILocalizer localizer, IUserLocale userLocale, IAppVersion appVersion)
		{
			_host = host;
			_dbContext = dbContext;
			_profiler = profiler;
			_codeProvider = codeProvider;
			_localizer = localizer;
			_userLocale = userLocale;
			_userStateManager = userStateManager;
			_appVersion = appVersion;
		}

		Int64 UserId => User.Identity.GetUserId<Int64>();
		Int32 TenantId => User.Identity.GetUserTenantId();

		public Boolean IsDebugConfiguration => _host.IsDebugConfiguration;

		public async Task Trace()
		{
			try
			{
				String json = _profiler.GetJson() ?? "{}";
				Response.ContentType = MimeTypes.Application.Json;
				await HttpResponseWritingExtensions.WriteAsync(Response, json, Encoding.UTF8);
			}
			catch (Exception /*ex*/)
			{
				//WriteExceptionStatus(ex);
				throw;
			}
		}

		public Task Script()
		{
			return DoScript(false);
		}

		public Task ScriptAdmin()
		{
			// TODO: is available?
			return DoScript(true);
		}

		async Task DoScript(Boolean admin)
		{
			try
			{
				Response.ContentType = MimeTypes.Application.Javascript;
				var script = await BuildScript(admin);
				await HttpResponseWritingExtensions.WriteAsync(Response, script, Encoding.UTF8);
			}
			catch (Exception ex)
			{
				Response.ContentType = MimeTypes.Text.Plain;
				Response.StatusCode = 500;
				await HttpResponseWritingExtensions.WriteAsync(Response, ex.ToString());
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
			prms.Set("UserId", UserId);
			if (_host.IsMultiTenant)
				prms.Set("TenantId", TenantId);
		}

		async Task<String> BuildScript(bool bAdmin)
		{
			String shell = bAdmin ? Resource.shellAdmin : Resource.shell;

			ExpandoObject loadPrms = new();
			SetSqlParams(loadPrms);

			var userInfo = User.Identity.UserInfo();

			var macros = new ExpandoObject();

			_ = macros.Append(new Dictionary<String, Object>
			{
				{ "AppVersion", _appVersion.AppVersion },
				{ "Admin", bAdmin ? "true" : "false" },
				{ "TenantAdmin", userInfo.IsTenantAdmin ? "true" : "false" },
				{ "Debug", IsDebugConfiguration ? "true" : "false" },
				{ "AppData", await GetAppData() },
				{ "Companies", "null" },
				{ "Period", "null" },
			});

			Boolean setCompany = false;
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

			if (_host.Mobile)
				loadPrms.Set("Mobile", true);


			String proc = bAdmin ? "a2admin.[Menu.Admin.Load]" : "a2ui.[Menu.User.Load]";
			IDataModel dm = await _dbContext.LoadModelAsync(_host.CatalogDataSource, proc, loadPrms);

			ExpandoObject menuRoot = dm.Root.RemoveEmptyArrays();
			SetUserStatePermission(dm);

			String jsonMenu = JsonConvert.SerializeObject(menuRoot, JsonHelpers.ConfigSerializerSettings(_host.IsDebugConfiguration));
			macros.Set("Menu", jsonMenu);

			if (setCompany)
			{
				var comps = dm.Root.Get<List<ExpandoObject>>("Companies");
				var currComp = comps?.Find(c => c.Get<Boolean>("Current"));

				if (currComp == null)
				{
					throw new InvalidDataException("There is no current company");
				}

				var menuJson = JsonConvert.SerializeObject(comps);
				macros.Set("Companies", $"{{menu:{menuJson}, links:null}}");

				_userStateManager.SetUserCompanyId(currComp.Get<Int64>("Id"));

			}

			return shell.ResolveMacros(macros);
		}

		async Task<MultiTenantParamJson> ProcessMultiTenantParams(ExpandoObject prms)
		{
			var permssionModel = await _dbContext.LoadModelAsync(_host.TenantDataSource, "a2security_tenant.[Permission.LoadMenu]", prms);
			if (permssionModel == null)
				return null;
			var root = permssionModel.Root;
			if (root == null)
				return null;

			// current company id
			Int64 currentCompanyId = root.Eval<Int64>("CurrentCompany.Id");
			if (currentCompanyId != 0)
				_userStateManager.SetUserCompanyId(currentCompanyId);

			// get keys and features
			StringBuilder strKeys = new();
			StringBuilder strFeatures = new();
			var modules = root.Eval<List<ExpandoObject>>("Modules");
			var features = root.Eval<List<ExpandoObject>>("Features");
			if (modules != null)
			{
				modules.ForEach(m =>
				{
					var key = m.Eval<String>("Module");
					if (key != null)
						strKeys.Append(key).Append(',');
				});
				if (strKeys.Length > 0)
					prms.Set("Keys", strKeys.RemoveTailComma().ToString());
				else
					prms.Set("Keys", "none"); // disable all
			}
			if (features != null)
			{
				features.ForEach(f =>
				{
					var feature = f.Eval<String>("Feature");
					if (feature != null)
						strFeatures.Append(feature).Append(',');
				});
				if (strFeatures.Length > 0)
					prms.Set("Features", strFeatures.RemoveTailComma().ToString());
				else
					prms.Set("Features", "____"); // all features disabled
			}

			// avaliable companies & xtra links
			var companies = root.Eval<List<ExpandoObject>>("Companies");
			var links = root.Eval<List<ExpandoObject>>("CompaniesLinks");
			var period = root.Eval<Object>("Period");
			if (companies != null || period != null)
			{
				String jsonCompanies = JsonConvert.SerializeObject(new { menu = companies, links },
					JsonHelpers.StandardSerializerSettings);
				String jsonPeriod = JsonConvert.SerializeObject(period, JsonHelpers.ConfigSerializerSettings(_host.IsDebugConfiguration));
				return new MultiTenantParamJson()
				{
					Companies = jsonCompanies,
					Period = jsonPeriod
				};
			}
			return null;
		}

		void SetUserStatePermission(IDataModel model)
		{
			if (_userStateManager == null)
				return;
			_userStateManager.SetReadOnly(model.Eval<Boolean>("UserState.ReadOnly"));
		}

		async Task<String> GetAppData()
		{
			var appJson = await _codeProvider.ReadTextFileAsync(String.Empty, "app.json", false);
			if (appJson != null)
			{
				ExpandoObject app = JsonConvert.DeserializeObject<ExpandoObject>(appJson);
				return _localizer.Localize(null, JsonConvert.SerializeObject(app));
			}

			ExpandoObject defAppData = new()
			{
				{ "version", _appVersion.AppVersion },
				{ "title", "A2v10.Core Web Application" },
				{ "copyright", _appVersion.Copyright }
			};
			return JsonConvert.SerializeObject(defAppData);
		}

		void GetAppFiles(String ext, TextWriter writer)
		{
			var files = _codeProvider.EnumerateFiles("_assets", $"*.{ext}", false);
			if (files == null)
				return;
			foreach (var f in files)
			{
				var fileName = f.ToLowerInvariant();
				if (!fileName.EndsWith($".min.{ext}"))
				{
					String minFile = fileName.Replace($".{ext}", $".min.{ext}");
					if (_codeProvider.FileExists(minFile))
						continue; // min.{ext} found
				}
				using var stream = _codeProvider.FileStreamFullPathRO(fileName);
				using var sr = new StreamReader(stream);
				var txt = sr.ReadToEnd();
				writer.Write(txt);
			}
		}

		#region IControllerLocale 
		public void SetLocale()
		{
			var locale = User.Identity.GetUserLocale();
			_userLocale.Locale = locale;
		}
		#endregion
	}
}

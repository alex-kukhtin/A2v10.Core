// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Core.Web.Mvc
{

	public class MultiTenantParamJson
	{
		public String Companies { get; set; }
		public String Period { get; set; }
	}

	public class ShellController : BaseController
	{

		public ShellController(IDbContext dbContext, IApplicationHost host, IAppCodeProvider codeProvider, 
			ILocalizer localizer, IUserStateManager userStateManager)
			: base(dbContext, host, codeProvider, localizer, userStateManager)
		{
		}

		public Boolean IsDebugConfiguration => _host.IsDebugConfiguration;

		[Route("{*pathInfo}")]
		public IActionResult Default(String pathInfo)
		{
			ViewBag.__Locale = "uk";
			ViewBag.__Build = 8000;
			ViewBag.__Minify = "min.";
			ViewBag.__Theme = "classic";
			ViewBag.__PersonName = "Person name";
			return View();
		}

		[Route("_shell/[action]")]
		public async Task Script()
		{
			Response.ContentType = "application/javascript";
			var script = await BuildScript(false);
			await HttpResponseWritingExtensions.WriteAsync(Response, script, Encoding.UTF8);
		}

		async Task<String> BuildScript(bool bAdmin)
		{
			String shell = bAdmin ? Resource.shellAdmin : Resource.shell;

			ExpandoObject loadPrms = new ExpandoObject();
			SetSqlQueryParamsWithoutCompany(loadPrms);

			var userInfo = User.Identity.UserInfo();

			var macros = new ExpandoObject();
			Boolean isUserIsAdmin = userInfo.IsAdmin && _host.IsAdminAppPresent;

			macros.Append(new Dictionary<String, Object>
			{
				{ "AppVersion", _host.AppVersion },
				{ "Admin", isUserIsAdmin ? "true" : "false" },
				{ "TenantAdmin", userInfo.IsTenantAdmin ? "true" : "false" },
				{ "Debug", IsDebugConfiguration ? "true" : "false" },
				{ "AppData", GetAppData() },
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
			StringBuilder strKeys = new StringBuilder();
			StringBuilder strFeatures = new StringBuilder();
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
						strFeatures.Append(feature).Append(",");
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

		String GetAppData()
		{
			/* TODO://
			var appJson = _host.ApplicationReader.ReadTextFile(String.Empty, "app.json");
			if (appJson != null)
			{
				// with validation
				ExpandoObject app = JsonConvert.DeserializeObject<ExpandoObject>(appJson);
				app.Set("embedded", _host.Embedded);
				return _localizer.Localize(null, JsonConvert.SerializeObject(app));
			}
			*/

			ExpandoObject defAppData = new ExpandoObject();
			defAppData.Set("version", _host.AppVersion);
			defAppData.Set("title", "A2v10.Core Web Application");
			defAppData.Set("copyright", _host.Copyright);
			defAppData.Set("embedded", _host.Embedded);
			return JsonConvert.SerializeObject(defAppData);
		}
	}
}

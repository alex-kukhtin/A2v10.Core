// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Text;

using Microsoft.Extensions.Configuration;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Platform.Web
{

	public record TenantInfo : ITenantInfo
	{
		public String Procedure => "[a2security].[SetTenantId]";
		public String ParamName => "@TenantId";

		public Int32 TenantId { get; init; }
	}

	public class WebApplicationHost : IApplicationHost, ITenantManager
	{
		private readonly IConfiguration _appSettings;
		private readonly IProfiler _profiler;
		private readonly IAppConfiguration _appConfiguration;
		private readonly PlatformOptions _options;
		private readonly IDbIdentity _currentUser;
		private Boolean _admin;

		public WebApplicationHost(IConfiguration config, IProfiler profiler, IAppConfiguration appConfiguration, PlatformOptions options, IDbIdentity currentUser)
		{
			_profiler = profiler;
			_appConfiguration = appConfiguration;

			_appSettings = config.GetSection("appSettings");
			_options = options;
			_currentUser = currentUser;
		}

		public Boolean IsMultiTenant => _options.MultiTenant;
		public Boolean IsMultiCompany => _options.MultiCompany;

		public Boolean IsDebugConfiguration => _appConfiguration.Debug;

		public Boolean IsUsePeriodAndCompanies => _appSettings.GetValue<Boolean>("custom");
		public Boolean IsRegistrationEnabled => _appSettings.GetValue<Boolean>("registration");
		public Boolean IsDTCEnabled => _appSettings.GetValue<Boolean>("enableDTC");

		public Boolean Mobile { get; private set; }

		public Boolean IsAdminMode => _admin;

		//public String AppDescription => throw new NotImplementedException();

		//public String AppHost => throw new NotImplementedException();
		//public String UserAppHost => throw new NotImplementedException();

		//public String SupportEmail => throw new NotImplementedException();
		public ITheme Theme => throw new NotImplementedException();

		//public String HelpUrl => throw new NotImplementedException();

		//public String HostingPath => throw new NotImplementedException();
		//public String SmtpConfig => throw new NotImplementedException();

		//public String ScriptEngine => throw new NotImplementedException();

		public Boolean IsAdminAppPresent => true /*TODO:*/;


		public String CatalogDataSource => IsMultiTenant ? "Catalog" : null;
		public String TenantDataSource => String.IsNullOrEmpty(_currentUser.Segment) ? null : _currentUser.Segment;

		public void CheckIsMobile(string host)
		{
			throw new NotImplementedException();
		}

		public String GetAppSettings(string source)
		{
			if (source == null)
				return null;
			if (!source.Contains("@{AppSettings.", StringComparison.InvariantCulture))
				return source;
			Int32 xpos = 0;
			var sb = new StringBuilder();
			do
			{
				Int32 start = source.IndexOf("@{AppSettings.", xpos);
				if (start == -1) break;
				Int32 end = source.IndexOf("}", start + 14);
				if (end == -1) break;
				var key = source.Substring(start + 14, end - start - 14);
				var value = _appSettings.GetValue<String>(key) ?? String.Empty;
				sb.Append(source[xpos..start]);
				sb.Append(value);
				xpos = end + 1;
			} while (true);
			sb.Append(source[xpos..]);
			return sb.ToString();
		}

		public ExpandoObject GetEnvironmentObject(string key)
		{
			throw new NotImplementedException();
		}

		/*
		public string MakeRelativePath(string path, string fileName)
		{
			throw new NotImplementedException();
		}
		*/

		public void SetAdmin(Boolean bAdmin)
		{
			_admin = bAdmin;
		}

		/*
		public void StartApplication(bool bAdmin)
		{
			throw new NotImplementedException();
		}
		*/

		#region ITenantManager
		public ITenantInfo GetTenantInfo(String source)
		{
			if (!IsMultiTenant)
				return null;
			if (source == CatalogDataSource)
				return null;
			if (!_currentUser.TenantId.HasValue)
				throw new InvalidOperationException("There is no TenantId");
			return new TenantInfo()
			{
				TenantId = _currentUser.TenantId.Value
			};
		}
		#endregion
	}
}

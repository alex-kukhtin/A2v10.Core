// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using System.Text;
using System.Data;
using System.Data.SqlClient;

using Microsoft.Extensions.Configuration;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

using SqlCommandType = System.Data.CommandType;

namespace A2v10.Core.Web.Mvc
{
	public class WebApplicationHost : IApplicationHost, ITenantManager
	{
		private readonly IConfiguration _appSettings;
		private readonly IProfiler _profiler;
		private readonly IAppConfiguration _appConfiguration;
		private Boolean _admin;

		public WebApplicationHost(IConfiguration config, IProfiler profiler, IAppConfiguration appConfiguration)
		{
			_profiler = profiler;
			_appConfiguration = appConfiguration;

			_appSettings = config.GetSection("appSettings");
		}

		public Boolean IsMultiTenant => _appSettings.GetValue<Boolean>("multiTenant");
		public Boolean IsMultiCompany => _appSettings.GetValue<Boolean>("multiCompany");

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

		public Boolean IsAdminAppPresent => false /*TODO:*/;

		public String CustomSecuritySchema => throw new NotImplementedException();


		public Int32? TenantId { get; set; }

		public Int64? UserId { get; set; }
		public String UserSegment { get; set; }

		public String CatalogDataSource => IsMultiTenant ? "Catalog" : null;
		public String TenantDataSource => String.IsNullOrEmpty(UserSegment) ? null : UserSegment;

		public String AppVersion => AppInfo.MainAssembly.Version;
		public String AppBuild => AppInfo.MainAssembly.Build;
		public String Copyright => AppInfo.MainAssembly.Copyright;

		public void CheckIsMobile(string host)
		{
			throw new NotImplementedException();
		}

		/*
		public string ConnectionString(string source)
		{
			throw new NotImplementedException();
		}
		*/

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

		const String SET_TENANT_CMD = "[a2security].[SetTenantId]";

		public async Task SetTenantIdAsync(IDbConnection cnn, String source)
		{
			if (!IsMultiTenant)
				return;
			if (source == CatalogDataSource)
				return;
			using var _ = _profiler.CurrentRequest.Start(ProfileAction.Sql, SET_TENANT_CMD);
			using var cmd = cnn.CreateCommand() as SqlCommand;
			cmd.CommandText = SET_TENANT_CMD;
			cmd.CommandType = SqlCommandType.StoredProcedure;
			cmd.Parameters.AddWithValue("@TenantId", TenantId);
			await cmd.ExecuteNonQueryAsync();
		}

		public void SetTenantId(IDbConnection cnn, String source)
		{
			if (!IsMultiTenant)
				return;
			if (source == CatalogDataSource)
				return;
			using var _ = _profiler.CurrentRequest.Start(ProfileAction.Sql, SET_TENANT_CMD);
			using var cmd = cnn.CreateCommand() as SqlCommand;
			cmd.CommandText = SET_TENANT_CMD;
			cmd.CommandType = SqlCommandType.StoredProcedure;
			cmd.Parameters.AddWithValue("@TenantId", TenantId);
			cmd.ExecuteNonQuery();
		}
		#endregion
	}
}

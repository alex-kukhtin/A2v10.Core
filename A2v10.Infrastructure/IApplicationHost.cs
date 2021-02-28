// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

namespace A2v10.Infrastructure
{

	public interface ITheme
	{
		String Name { get; }
		String FileName { get; }
		String ColorScheme { get; }
	}

	public interface IApplicationHost
	{
		//IProfiler Profiler { get; }

		String AppPath { get; }
		String AppKey { get; }

		Boolean Mobile { get; }
		Boolean Embedded { get; }
		Boolean IsAdminMode { get; }

		ITheme Theme { get; }

		Boolean IsDebugConfiguration { get; }
		Boolean IsRegistrationEnabled { get; }
		Boolean IsAdminAppPresent { get; }

		Boolean IsMultiTenant { get; }
		Boolean IsMultiCompany { get; }
		Boolean IsUsePeriodAndCompanies { get; }

		Int32? TenantId { get; set; }
		Int64? UserId { get; set; }
		String UserSegment { get; set; }

		String CatalogDataSource { get; }
		String TenantDataSource { get; }

		String AppVersion { get; }
		String AppBuild { get; }
		String Copyright { get; }

		String GetAppSettings(String source);
		ExpandoObject GetEnvironmentObject(String key);

		void CheckIsMobile(String host);
	}
}

// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Dynamic;

namespace A2v10.Infrastructure;
public interface IApplicationHost
{
	Boolean Mobile { get; }
	Boolean IsAdminMode { get; }

	Boolean IsDebugConfiguration { get; }
	//Boolean IsProductionEnvironment { get; }
	Boolean IsRegistrationEnabled { get; }
	Boolean IsAdminAppPresent { get; }

	Boolean IsMultiTenant { get; }
	Boolean IsMultiCompany { get; }
	Boolean IsUsePeriodAndCompanies { get; }

	String? CatalogDataSource { get; }
	String? TenantDataSource { get; }

	String? GetAppSettings(String? source);
	ExpandoObject GetEnvironmentObject(String key);

	void CheckIsMobile(String host);
}


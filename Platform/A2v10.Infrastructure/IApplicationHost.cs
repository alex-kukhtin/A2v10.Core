// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

namespace A2v10.Infrastructure;
public interface IApplicationHost
{
	Boolean Mobile { get; }

	Boolean IsDebugConfiguration { get; }
	//Boolean IsProductionEnvironment { get; }
	Boolean IsMultiTenant { get; }
	Boolean IsMultiCompany { get; }

	String? TenantDataSource { get; }

	String? GetAppSettings(String? source);
	ExpandoObject GetEnvironmentObject(String key);
}


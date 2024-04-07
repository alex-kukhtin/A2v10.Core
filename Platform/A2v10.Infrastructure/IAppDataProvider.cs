// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public interface IAppDataProvider
{
	Task<ExpandoObject> GetAppDataAsync();
	Task<String> GetAppDataAsStringAsync();

	String AppVersion { get; }
}

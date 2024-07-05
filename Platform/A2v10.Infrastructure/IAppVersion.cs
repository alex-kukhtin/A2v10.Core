// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure;

public interface IAppVersion
{
	String AppVersion { get; }
	String AppBuild { get; }
	String Copyright { get; }
	String? ModuleVersion { get; }
}

// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Infrastructure;

public record AppEnvironment
{
	private const String DEBUG = "debug";
	public Boolean Watch { get; set; }
	public String Stage { get; set; } = DEBUG;
	public Boolean IsDebug => Stage == DEBUG;
	public Boolean IsRelease => !IsDebug;
}

public record ModuleInfo
{
    public String? Path { get; init; }
    public Boolean Default { get; init; }
}

public record AppOptions
{
	public String AppId { get; set; } = "app.main";
	public String Path { get; set; } = "undefined";
	public String AppName { get; set; } = String.Empty;
	public String? UserMenu { get; set; }
	public String? Theme { get; set; }
	public String? Layout { get; set; }
	public String? HelpUrl { get; set; }
	public Boolean MultiTenant { get; set; }
	public Boolean MultiCompany { get; set; }
	public Boolean Registration { get; set; }
	public String? CookiePrefix { get; set; }
    public AppEnvironment Environment { get; } = new AppEnvironment();
	public Dictionary<String, ModuleInfo>? Modules { get; set; }
	public Boolean IsCustomUserMenu => !String.IsNullOrEmpty(UserMenu);
	public List<String> SinglePages { get; set; } = [];
}

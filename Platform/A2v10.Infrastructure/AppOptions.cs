// Copyright © 2015-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Infrastructure;

public sealed record AppEnvironment
{
	private const String DEBUG = "debug";
	public Boolean Watch { get; set; }
	public String Stage { get; set; } = DEBUG;
	public Boolean IsDebug => Stage == DEBUG;
	public Boolean IsRelease => !IsDebug;
}

public enum ClientFramework
{
	Vue2, // first: a host without the key stays on Vue 2
	Vue3
}

public sealed record ModuleInfo
{
    public String? Path { get; init; }
    public String? Assembly { get; init; }
    public Boolean Default { get; init; }
    public String? RealPath => Assembly != null ? $"clr-type:{Assembly}.AppContainer;assembly={Assembly}" : Path;
}

public sealed record AppOptions
{
	public String AppId { get; set; } = "app.main";
	public String Path { get; set; } = "undefined";
	public String AppName { get; set; } = String.Empty;
	public String? UserMenu { get; set; }
	public String? Theme { get; init; }
	public String? DarkTheme { get; init; }
    public String? Layout { get; set; }
	public ClientFramework Framework { get; init; }
    public String? BodyCssClass { get; set; }
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

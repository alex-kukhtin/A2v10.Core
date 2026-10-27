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
	// the suffix of a delivered script: every layout asks for the same URL, so the browser's cache holds one file
	public String Minify => IsRelease ? "min." : String.Empty;
}

public enum ClientFramework
{
	Vue2, // first: a host without the key stays on Vue 2
	Vue3
}

public static class ClientFrameworkExtensions
{
	// a view that carries Vue: Vue 2 keeps its names, Vue 3 takes a suffix; a missing view fails loud, never falls back to Vue 2
	public static String ViewName(this ClientFramework framework, String name) => framework switch
	{
		ClientFramework.Vue2 => name,
		ClientFramework.Vue3 => $"{name}.vue3",
		_ => throw UnsupportedFramework(framework)
	};

	// "Framework": 3 binds as (ClientFramework)3, not as Vue3
	public static InvalidOperationException UnsupportedFramework(ClientFramework framework) =>
		new($"Application:Framework = '{(Int32)framework}' is not supported; use vue2 | vue3");
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

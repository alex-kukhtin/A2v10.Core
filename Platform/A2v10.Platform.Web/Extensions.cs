// Copyright © 2020-2022 Alex Kukhtin. All rights reserved.

using System;


using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

using A2v10.Platform.Web;
using A2v10.Platform.Web.Controllers;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceExtensions
{
	public static IMvcBuilder AddPlatformCore(this IServiceCollection services)
	{
		var webMvcAssembly = typeof(ShellController).Assembly;
		var builder = services.AddControllersWithViews()
			.AddApplicationPart(webMvcAssembly);

		/*
		services.Configure<MvcRazorRuntimeCompilationOptions>(opts => {
			opts.FileProviders.Add(new EmbeddedFileProvider(webMvcAssembly));
		});
		*/

		services.AddSingleton<IApplicationTheme, WebApplicationTheme>();

		services.AddScoped<IApplicationHost, WebApplicationHost>();
		services.AddScoped<ITenantManager, WebTenantManager>();

		services.AddScoped<WebLocalizer>()
			.AddScoped<ILocalizer>(s => s.GetRequiredService<WebLocalizer>())
			.AddScoped<IDataLocalizer>(s => s.GetRequiredService<WebLocalizer>());

		services.AddScoped<WebProfiler>()
			.AddScoped<IProfiler>(s => s.GetRequiredService<WebProfiler>())
			.AddScoped<IDataProfiler>(s => s.GetRequiredService<WebProfiler>());

		services.AddSingleton<ILocalizerDictiorany, WebLocalizerDictiorany>();
		services.AddScoped<IAppDataProvider, WebAppDataProvider>();

		services.AddScoped<ITokenProvider, WebTokenProvider>();

		services.AddScoped<CurrentUser>()
			.AddScoped<ICurrentUser>(s => s.GetRequiredService<CurrentUser>())
			.AddScoped<IDbIdentity>(s => s.GetRequiredService<CurrentUser>());

		services.AddDistributedMemoryCache();
		services.AddSession();

		return builder;
	}

	public static IServiceCollection AddViewEngines(this IServiceCollection services, Action<ViewEngineFactory> action)
	{
		var viewEngineFactory = new ViewEngineFactory();
		action.Invoke(viewEngineFactory);
		foreach (var e in viewEngineFactory.Engines)
			services.AddScoped(e.EngineType);

		services.AddScoped<IViewEngineProvider>(s =>
			new WebViewEngineProvider(s, viewEngineFactory.Engines)
		);
		return services;
	}

	public static IServiceCollection AddReportEngines(this IServiceCollection services, Action<ReportEngineFactory> action)
	{
		var reportEngineFactory = new ReportEngineFactory();
		action.Invoke(reportEngineFactory);
		foreach (var r in reportEngineFactory.Engines)
			services.AddScoped(r.EngineType);

		services.AddScoped<IReportEngineProvider>(s =>
			new WebReportEngineProvider(s, reportEngineFactory.Engines)
		);
		return services;
	}

	public static IServiceCollection AddInvokeTargets(this IServiceCollection services, Action<InvokeEngineFactory> action)
	{
		var invokeEngineFactory = new InvokeEngineFactory();
		action.Invoke(invokeEngineFactory);

		foreach (var r in invokeEngineFactory.Engines)
		{
			switch (r.Scope)
			{
				case InvokeScope.Scoped:
					services.AddScoped(r.EngineType);
					break;
				case InvokeScope.Singleton:
					services.AddSingleton(r.EngineType);
					break;
				case InvokeScope.Transient:
					services.AddTransient(r.EngineType);
					break;
				default:
					throw new InvalidProgramException($"Invalid Invoke scope: {r.Scope}");
			}
		}

		services.AddScoped<IInvokeEngineProvider>(s =>
			new WebInvokeEngineProvider(s, invokeEngineFactory.Engines)
		);
		return services;
	}
}

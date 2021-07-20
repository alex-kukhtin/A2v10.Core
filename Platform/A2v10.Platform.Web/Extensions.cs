// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;


using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.FileProviders;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

using A2v10.Platform.Web;
using A2v10.Platform.Web.Controllers;

namespace Microsoft.Extensions.DependencyInjection
{
	public static class ServiceExtensions
	{
		public static IMvcBuilder AddPlatformCore(this IServiceCollection services, Action<PlatformOptions> options = null)
		{
			var platformOptions = new PlatformOptions();
			options?.Invoke(platformOptions);

			services.AddSingleton<PlatformOptions>(s => platformOptions);

			var webMvcAssembly = typeof(ShellController).Assembly;
			var builder = services.AddControllersWithViews()
				.AddApplicationPart(webMvcAssembly);

			services.Configure<MvcRazorRuntimeCompilationOptions>(opts => {
				opts.FileProviders.Add(new EmbeddedFileProvider(webMvcAssembly));
			});

			services.AddSingleton<IAppCodeProvider, FileSystemCodeProvider>();

			services.AddScoped<WebApplicationHost>();

			services.AddScoped<WebLocalizer>()
				.AddScoped<ILocalizer>(s => s.GetService<WebLocalizer>())
				.AddScoped<IDataLocalizer>(s => s.GetService<WebLocalizer>());

			services.AddScoped<WebProfiler>()
				.AddScoped<IProfiler>(s => s.GetService<WebProfiler>())
				.AddScoped<IDataProfiler>(s => s.GetService<WebProfiler>());

			services.AddScoped<IApplicationHost>(s => s.GetService<WebApplicationHost>());

			services.AddSingleton<ILocalizerDictiorany, WebLocalizerDictiorany>();

			services.AddScoped<ITokenProvider, WebTokenProvider>();

			services.AddScoped<CurrentUser>()
				.AddScoped<ICurrentUser>(s => s.GetService<CurrentUser>());

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
	}
}

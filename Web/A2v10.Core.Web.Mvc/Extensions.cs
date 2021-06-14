// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;
using System.IO;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.FileProviders;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Core.Web.Mvc
{

	public static class ServiceExtensions
	{
		public static IMvcBuilder AddPlatformCore(this IServiceCollection services)
		{
			var webMvcAssembly = typeof(ShellController).Assembly;
			var builder = services.AddControllersWithViews()
				.AddApplicationPart(webMvcAssembly);

			services.Configure<MvcRazorRuntimeCompilationOptions>(opts =>
				opts.FileProviders.Add(new EmbeddedFileProvider(webMvcAssembly)));

			services.AddSingleton<IAppCodeProvider>(s =>
				CreateCodeProvider(
					s.GetService<IConfiguration>(),
					s.GetService<IWebHostEnvironment>())
			);

			services.AddScoped<WebApplicationHost>();

			services.AddScoped<IUserLocale, WebUserLocale>()
				.AddScoped<WebLocalizer>()
				.AddScoped<ILocalizer>(s => s.GetService<WebLocalizer>())
				.AddScoped<IDataLocalizer>(s => s.GetService<WebLocalizer>());

			services.AddScoped<WebProfiler>()
				.AddScoped<IProfiler>(s => s.GetService<WebProfiler>())
				.AddScoped<IDataProfiler>(s => s.GetService<WebProfiler>());

			services.AddScoped<IApplicationHost>(s => s.GetService<WebApplicationHost>());
			services.AddSingleton<ILocalizerDictiorany, WebLocalizerDictiorany>();

			services.AddScoped<IUserStateManager>(s =>
				new WebUserStateManager(s.GetService<IHttpContextAccessor>()));
			services.AddScoped<ITokenProvider, WebTokenProvider>();

			services.AddSession();

			return builder;
		}

		static IAppCodeProvider CreateCodeProvider(IConfiguration config, IWebHostEnvironment webHost)
		{
			var appSection = config.GetSection("application");
			var appPath = appSection.GetValue<String>("path");
			var appKey = appSection.GetValue<String>("name");

			if (appPath.StartsWith("db:"))
				throw new NotImplementedException("DB: AppCodeProvider");

			var zipFileName = Path.ChangeExtension(Path.Combine(appPath, appKey), "zip");
			if (File.Exists(zipFileName))
				throw new NotImplementedException("ZIP: AppCodeProvider");

			return new FileSystemCodeProvider(webHost, appPath, appKey);
		}

		public static void AddViewEngines(this IServiceCollection services, Action<ViewEngineFactory> action)
		{
			var viewEngineFactory = new ViewEngineFactory();
			action.Invoke(viewEngineFactory);
			foreach (var e in viewEngineFactory.Engines)
				services.AddScoped(e.EngineType);
			services.AddScoped<IViewEngineProvider>(s =>
				new WebViewEngineProvider(s, viewEngineFactory.Engines)
			);
		}
	}
}

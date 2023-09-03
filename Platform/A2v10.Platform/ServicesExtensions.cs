// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Globalization;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;

using A2v10.Data;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Services;

using A2v10.ViewEngine.Xaml;
using A2v10.ViewEngine.Html;

using A2v10.Platform.Web;
using A2v10.Module.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
	public static IServiceCollection UseSqlServerStorage(this IServiceCollection services, IConfiguration configuration)
	{
		// Storage
		services.AddOptions<DataConfigurationOptions>();

		services.AddScoped<IDbContext, SqlDbContext>()
			.AddSingleton<MetadataCache>()
			.AddSingleton<IDataConfiguration, DataConfiguration>();

		services.Configure<DataConfigurationOptions>(opts =>
		{
			opts.ConnectionStringName = "Default";
			opts.DisableWriteMetadataCaching = !configuration.GetValue<Boolean>("Data:MetadataCache");
			var to = configuration.GetValue<String>("Data:CommandTimeout");
			if (to != null)
			{
				if (TimeSpan.TryParse(to, out TimeSpan timeout))
					opts.DefaultCommandTimeout = timeout;
				else
					throw new FormatException("Data:CommandTimeout. Invalid TimeSpan format");
			}
		});
		return services;
	}

	public static IMvcBuilder UsePlatform(this IServiceCollection services, IConfiguration configuration)
	{
		var builder = services.AddPlatformCore()
			.AddDefaultIdentityUI();

		services.AddSingleton<IAppCodeProvider, AppCodeProvider>();
		services.AddSingleton<IModelJsonPartProvider, ModelJsonPartProvider>();
		services.AddSingleton<IXamlPartProvider, XamlPartProvider>();

		// default implementations
		services.TryAddSingleton<IMailService, NullMailService>();
		services.TryAddScoped<IUserBannerProvider, NullUserBannerProvider>();
		services.TryAddScoped<ILicenseManager, EmptyLicenseManager>();
		services.TryAddSingleton<ISqlQueryTextProvider, NullSqlQueryTextProvider>();

		var cookiePrefix = configuration.GetValue<String>("identity:cookiePrefix")?.Trim()
			?? "A2v10Platform";

		services.AddPlatformIdentityCore<Int64>()
			.AddIdentityConfiguration<Int64>(configuration)
			.AddPlatformAuthentication(cookiePrefix);

		services.AddSingleton<IWebHostFilesProvider, WebHostFilesProvider>();

		services.UseSqlServerStorage(configuration);

		services.AddViewEngines(x =>
		{
			x.RegisterEngine<XamlViewEngine>(".xaml");
			x.RegisterEngine<HtmlViewEngine>(".html");
		});

		// Platform services
		services.Configure<AppOptions>(opts =>
		{
			configuration.GetSection("application").Bind(opts);
			opts.CookiePrefix = cookiePrefix;
			opts.Modules = configuration.GetSection("application:modules")
				.GetChildren().ToDictionary<IConfigurationSection, String, ModuleInfo>(
					x => x.Key,
					x =>
					{
						var mi = new ModuleInfo();
						x.Bind(mi);
						return mi;
					},
					StringComparer.InvariantCultureIgnoreCase);
		});

		services.AddScoped<IDataService, DataService>();
		services.AddScoped<IModelJsonReader, ModelJsonReader>();
		services.AddScoped<IReportService, ReportService>();

		// platfrom core services
		services.AddSingleton<IAppVersion, PlatformAppVersion>();

		services.AddHttpClient();
		services.AddSignalR();

		return builder;
	}

	public static void ConfigurePlatform(this IApplicationBuilder app, IWebHostEnvironment env)
	{
		if (env.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
		}
		else
		{
			app.UseExceptionHandler("/main/error");
			// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
			app.UseHsts();
		}
		app.UseHttpsRedirection();

		app.UseStaticFiles();
		app.UseRouting();
		app.UseAuthentication();
		app.UseAuthorization();
		app.UseSession();

		app.UseCookiePolicy(new CookiePolicyOptions()
		{
			HttpOnly = HttpOnlyPolicy.Always,
			Secure = CookieSecurePolicy.Always,
			MinimumSameSitePolicy = SameSiteMode.Strict
		});

		app.UseMiddleware<CurrentUserMiddleware>();


		app.UseEndpoints(endpoints =>
		{
			endpoints.MapControllers();
			endpoints.MapHub<DefaultHub>("/_userhub");
		});

		// TODO: use settings?
		var cultureInfo = new CultureInfo("uk-UA");

		CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
		CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
	}
}



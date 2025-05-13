// Copyright © 2021-2025 Oleksandr Kukhtin. All rights reserved.

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
using Microsoft.AspNetCore.Authentication;

using A2v10.Data;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Services;

using A2v10.ViewEngine.Xaml;
using A2v10.ViewEngine.Html;

using A2v10.Platform.Web;
using A2v10.Data.Providers;
using A2v10.Platform;

namespace Microsoft.Extensions.DependencyInjection;

public record Builders(IMvcBuilder MvcBuilder, AuthenticationBuilder AuthenticationBuilder);

public static class ServicesExtensions
{
	public static IServiceCollection UseSqlServerStorage(this IServiceCollection services, IConfiguration configuration)
	{
		// Storage
		services.AddOptions<DataConfigurationOptions>();

		services.AddScoped<IDbContext, SqlDbContext>()
			.AddSingleton<MetadataCache>()
			.AddSingleton<IDataConfiguration, DataConfiguration>();
		services.AddSingleton<IStaticDbContext, StaticDbContext>();

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

	public static Builders UsePlatform(this IServiceCollection services, IConfiguration configuration)
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
		services.TryAddScoped<IAppRuntimeBuilder, NullAppRuntimeBuilder>();
		services.TryAddSingleton<IPermissionBag, NullPermissionBag>();

		var cookiePrefix = configuration.GetValue<String>("identity:cookiePrefix")?.Trim()
			?? "A2v10Platform";

		var authBuilder = services.AddPlatformIdentityCore<Int64>()
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
			var strModules = configuration.GetValue<String>("application:modules");

			if (strModules != null)
			{
				opts.Modules = OptionsExtensions.ModulesFromString(strModules);
			}
			else
			{
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
			}
		});

		services.AddScoped<IDataService, DataService>();
		services.AddScoped<IModelJsonReader, ModelJsonReader>();
		services.AddScoped<IReportService, ReportService>();
		services.AddScoped<IExternalDataProvider, ExternalDataContext>();

		// platfrom core services
		services.AddSingleton<IAppVersion, PlatformAppVersion>();

		services.AddTransient<IBackgroundProcessHandler, BackgroundProcessHandler>();

		services.AddHttpClient();
		services.AddSignalR();

		return new Builders(builder, authBuilder);
	}

	public static void ConfigurePlatform(this IApplicationBuilder app, IWebHostEnvironment env, IConfiguration configuration = null)
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
			MinimumSameSitePolicy = SameSiteMode.Lax /* Required for GOOGLE Auth*/
		});

		app.UseMiddleware<CurrentUserMiddleware>();


		app.UseEndpoints(endpoints =>
		{
			endpoints.MapControllers();
			endpoints.MapHub<DefaultHub>("/_userhub");
		});

		var cultureInfo = new CultureInfo("uk-UA");
		if (configuration != null)
		{
			var ci = configuration.GetValue<String>("Globalization:Locale");
			if (!String.IsNullOrEmpty(ci))
				cultureInfo = new CultureInfo(ci);
		}

		CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
		CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
	}


	public static IServiceCollection UsePermissions(this IServiceCollection services)
	{
		return services.AddSingleton<IPermissionBag, WebPermissonBug>();
	}
}



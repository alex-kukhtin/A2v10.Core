// Copyright © 2021-2022 Alex Kukhtin. All rights reserved.

using System;
using System.Globalization;

using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using A2v10.Data;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Services;

using A2v10.ViewEngine.Xaml;
using A2v10.ViewEngine.Html;

using A2v10.Platform.Web;

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

		var appPath = configuration.GetValue<String>("application:path").Trim();
		Boolean isClr = appPath.StartsWith("clr-type:");

		var cookiePrefix = configuration.GetValue<String>("identity:cookiePrefix").Trim();
	
		services.AddPlatformIdentityCore<Int64>()
			.AddIdentityConfiguration<Int64>(configuration)
			.AddPlatformAuthentication(cookiePrefix);

		services.AddSingleton<IWebHostFilesProvider, WebHostFilesProvider>();
		if (isClr)
		{
			services.AddSingleton<IAppProvider, AppProvider>();
			services.AddSingleton<IAppCodeProvider, ClrCodeProvider>();
			services.AddSingleton<IModelJsonPartProvider, ModelJsonPartProviderClr>();
			services.AddSingleton<IXamlPartProvider, XamlPartProviderClr>();
		}
		else /* Is File System */
		{
			services.AddSingleton<IAppCodeProvider, FileSystemCodeProvider>();
			services.AddSingleton<IModelJsonPartProvider, ModelJsonPartProviderFile>();
			services.AddSingleton<IXamlPartProvider, XamlPartProviderFile>();
		}

		services.UseSqlServerStorage(configuration);

		services.AddViewEngines(x =>
		{
			x.RegisterEngine<XamlViewEngine>(".xaml");
			x.RegisterEngine<HtmlViewEngine>(".html");
		});

		// Platform services
		services.Configure<AppOptions>(
			configuration.GetSection("Application")
		);

		services.AddScoped<IDataService, DataService>();
		services.AddScoped<IModelJsonReader, ModelJsonReader>();
		services.AddScoped<IReportService, ReportService>();

		// platfrom core services
		services.AddSingleton<IAppVersion, PlatformAppVersion>();

		services.AddHttpClient();

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
			app.UseExceptionHandler("/home/error");
			// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
			app.UseHsts();
		}
		app.UseHttpsRedirection();

		app.UseStaticFiles();

		app.UseRouting();
		app.UseAuthentication();
		app.UseAuthorization();
		app.UseSession();

		app.UseMiddleware<CurrentUserMiddleware>();


		app.UseEndpoints(endpoints =>
		{
			endpoints.MapControllers();
		});

		// TODO: use settings?
		var cultureInfo = new CultureInfo("uk-UA");

		CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
		CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
	}
}


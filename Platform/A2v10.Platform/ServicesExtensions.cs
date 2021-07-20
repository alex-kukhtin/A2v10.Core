// Copyright © 2021 Alex Kukhtin. All rights reserved.

using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using A2v10.Infrastructure;
using A2v10.ReportEngine.Stimulsoft;
using A2v10.Services;
using A2v10.ViewEngine.Xaml;
using A2v10.Platform.Web;

namespace Microsoft.Extensions.DependencyInjection
{
	public static class ServicesExtensions
	{
		public static IServiceCollection UsePlatform(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddPlatformCore(opts =>
			{
				// default values
				//opts.MultiTenant = false;
				//opts.MultiCompany = false;
				//opts.GlobalPeriod = false;
			})
			.AddDefaultIdentityUI()
			.AddStimulsoftUI();

			services.AddPlatformIdentityCore(opts =>
			{
				//opts.DataSource = "Catalog";
				//opts.Schema = "mySchema";
			})
			.AddPlatformAuthentication();

			services.AddSqlServerStorage();

			services.AddViewEngines(x =>
			{
				x.RegisterEngine<XamlViewEngine>(".xaml");
			})
			.AddReportEngines(x =>
			{
				x.RegisterEngine<StimulsoftReportEngine>("stimulsoft");
			})
			.AddStimulsoftLicense(configuration);

			// Platform services
			services.AddSingleton<IAppConfiguration, AppConfiruation>();
			services.AddScoped<IDataService, DataService>();
			services.AddScoped<IModelJsonReader, ModelJsonReader>();
			services.AddScoped<IReportService, ReportService>();

			// platfrom core services
			services.AddSingleton<IAppVersion, PlatformAppVersion>();

			return services;
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
		}
	}
}

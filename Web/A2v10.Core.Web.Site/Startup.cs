// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using A2v10.Data.Interfaces;
using A2v10.Data;
using A2v10.Web.Identity;
using A2v10.Core.Web.Mvc;
using A2v10.Infrastructure;
using A2v10.Xaml;
using A2v10.Services;
using A2v10.Stimulsoft.Interop;
using A2v10.Data.Config;

namespace A2v10.Core.Web.Site
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddPlatformCore();

			services.AddPlatformIdentity();

			services.AddViewEngines(x =>
			{
				x.RegisterEngine<XamlViewEngine>(".xaml");
			});

			// Storage
			services.AddScoped<IDbContext>(s =>
				new SqlDbContext(s.GetService<IDataProfiler>(),
					new DataConfiguration(Configuration, opts =>
					{
						opts.ConnectionStringName = "Default";
					}),
					s.GetService<IDataLocalizer>(),
					s.GetService<ITenantManager>(),
					s.GetService<ITokenProvider>()
				)
			);

			// Services
			services.AddSingleton<IAppConfiguration, AppConfiruation>();
			services.AddScoped<IDataService, DataService>();
			services.AddScoped<IModelJsonReader, ModelJsonReader>();

			// reports
			services.AddScoped<IReportService, ReportService>();
			services.AddSingleton<IExternalReport, StimulsoftExternalReport>();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
				/*
				endpoints.MapControllerRoute(
					name: "internal",
					pattern: "_{controller}/{*pathInfo}",
					defaults: new { action = "default" }
				);

				endpoints.MapControllerRoute(
					name: "default",
					pattern: "_shell/{action}/{*pathInfo}",
					defaults: new { controller = "shell"}
				);

				endpoints.MapControllerRoute(
					name: "default",
					pattern: "{*pathInfo}",
					defaults: new { controller="shell", action = "default" }
				);
				*/

				//endpoints.MapRazorPages();
			});
		}
	}
}

// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using A2v10.Infrastructure;
using A2v10.Services;

using A2v10.ViewEngine.Xaml;

using A2v10.ReportEngine.Stimulsoft;

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
			var sect = Configuration.GetSection("A2v10:configuration");
			var mt = sect.GetValue<Boolean>("multiTenant");

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
			});

			// Services
			services.AddSingleton<IAppConfiguration, AppConfiruation>();
			services.AddScoped<IDataService, DataService>();
			services.AddScoped<IModelJsonReader, ModelJsonReader>();
			services.AddScoped<IReportService, ReportService>();
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
			});
		}
	}
}

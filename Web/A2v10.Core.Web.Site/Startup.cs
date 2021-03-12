
using System;
using System.IO;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http;

using A2v10.Data.Interfaces;
using A2v10.Data;
using A2v10.Web.Identity;
using A2v10.Core.Web.Mvc;
using A2v10.Infrastructure;
using A2v10.System.Xaml;
using A2v10.Xaml;
using A2v10.Services;
using A2v10.Stimulsoft.Interop;

namespace A2v10.Core.Web.Site
{
	public class DataConfiguration : IDataConfiguration
	{
		public TimeSpan CommandTimeout => TimeSpan.FromSeconds(30);

		private readonly IConfiguration _config;

		public DataConfiguration(IConfiguration config)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
		}

		public String ConnectionString(String source)
		{
			if (String.IsNullOrEmpty(source))
				source = "Default";
			return _config.GetConnectionString(source);
		}
	}

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
			var webMvcAssembly = typeof(ShellController).Assembly;
			services.AddControllersWithViews()
				.AddApplicationPart(webMvcAssembly);

			services.Configure<MvcRazorRuntimeCompilationOptions>(opts =>
				opts.FileProviders.Add(new EmbeddedFileProvider(webMvcAssembly)));

			services.AddPlatformIdentity();

			// TODO: GetLocalize
			services.AddSingleton<WebLocalizer>(s => 
				new WebLocalizer(
					s.GetService<IAppCodeProvider>(), 
					s.GetService<IAppConfiguration>(),
					"uk-UA")
			)
			.AddSingleton<ILocalizer>(s => s.GetService<WebLocalizer>())
			.AddSingleton<IDataLocalizer>(s => s.GetService<WebLocalizer>());

			services.AddSingleton<IAppCodeProvider>(s => 
				CreateCodeProvider(Configuration, 
				s.GetService<IWebHostEnvironment>())
			);

			services.AddSingleton<IXamlReaderService, AppXamlReaderService>();

			services.AddScoped<WebProfiler>();
			services.AddScoped<WebApplicationHost>();

			services.AddScoped<IProfiler>(s => s.GetService<WebProfiler>());
			services.AddScoped<IDataProfiler>(s=> s.GetService<WebProfiler>());
			services.AddScoped<ITenantManager>(s => s.GetService<WebApplicationHost>());
			services.AddScoped<IApplicationHost>( s=> s.GetService<WebApplicationHost>());

			services.AddScoped<IDbContext>(s => 
				new SqlDbContext(s.GetService<IDataProfiler>(), 
					new DataConfiguration(Configuration), 
					s.GetService<IDataLocalizer>(),
					s.GetService<ITenantManager>(),
					s.GetService<ITokenProvider>()
				)
			);
			services.AddScoped<IUserStateManager>(s => 
				new WebUserStateManager(s.GetService<IHttpContextAccessor>()));

			services.AddScoped<ITokenProvider, WebTokenProvider>();
			services.AddScoped<IDataService, DataService>();
			services.AddScoped<IReportService, ReportService>();
			services.AddSingleton<IExternalReport, StimulsoftExternalReport>();
			services.AddSingleton<IModelJsonReader, ModelJsonReader>();
			services.AddSingleton<IAppConfiguration, AppConfiruation>();

			services.AddSession();
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

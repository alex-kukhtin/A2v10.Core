
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using A2v10.Data.Interfaces;
using A2v10.Data;
using A2v10.Web.Identity;
using System.Reflection;
using A2v10.Core.Web.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.FileProviders;
using A2v10.Infrastructure;
using Microsoft.AspNetCore.Http;

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

		public string ConnectionString(String source)
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
			//services.AddDbContext<ApplicationDbContext>(options =>
			//options.UseSqlServer(
			//Configuration.GetConnectionString("DefaultConnection")));
			//services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
			//.AddEntityFrameworkStores<ApplicationDbContext>();

			var webMvcAssembly = typeof(ShellController).Assembly;
			services.AddControllersWithViews()
				.AddApplicationPart(webMvcAssembly);

			services.Configure<MvcRazorRuntimeCompilationOptions>(opts =>
				opts.FileProviders.Add(new EmbeddedFileProvider(webMvcAssembly)));

			//services.AddRazorPages();
			services.AddIdentity<AppUser, AppRole>(options =>
			{
				options.Lockout.MaxFailedAccessAttempts = 5;
				options.SignIn.RequireConfirmedEmail = true;

				options.User.RequireUniqueEmail = true;
			})
			.AddUserStore<AppUserStore>()
			.AddUserManager<UserManager<AppUser>>()
			.AddRoleStore<AppRoleStore>()
			.AddRoleManager<RoleManager<AppRole>>()
			.AddSignInManager<SignInManager<AppUser>>()
			.AddDefaultTokenProviders();

			services.ConfigureApplicationCookie(opts =>
			{
				opts.LoginPath = "/account/login";
				opts.ReturnUrlParameter = "returnurl";
			});

			services.AddScoped<IApplicationHost>(provider => new WebApplicationHost(Configuration));
			services.AddScoped<IDbContext>(provider => 
				new SqlDbContext(new NullDataProfiler(), 
				new DataConfiguration(Configuration), 
				new NullDataLocalizer())
			);
			services.AddScoped<IAppCodeProvider>(provider => new WebAppCodeProvider(Configuration));
			services.AddScoped<IUserStateManager>(provider => 
				new WebUserStateManager(provider.GetService<IHttpContextAccessor>()));

			services.AddSession();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				//app.UseDatabaseErrorPage();
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

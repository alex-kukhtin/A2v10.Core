// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.WorkflowEngine;

namespace A2v10.Core.Web.Site
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			services.UsePlatform(Configuration);

			/*
			services.Configure<IdentityOptions>(opts =>
			{
				var pwd = opts.Password;
				pwd.RequireDigit = false;
				pwd.RequiredLength = 1;
				pwd.RequireLowercase = false;
				pwd.RequireUppercase = false;
				pwd.RequireNonAlphanumeric = false;

				var si = opts.SignIn;
				si.RequireConfirmedEmail = false;
				si.RequireConfirmedAccount = false;
				si.RequireConfirmedPhoneNumber = false;

				var us = opts.User;
				us.RequireUniqueEmail = false;
			});
			*/

			services.UseWorkflowEngine();
			services.AddInvokeTargets(a =>
			{
				a.RegisterEngine<WorkflowInvokeTarget>("Workflow", InvokeScope.Scoped);
			});
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.ConfigurePlatform(env);
		}
	}
}

// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;

using A2v10.Infrastructure;
using A2v10.WorkflowEngine;
using A2v10.Web.Identity;

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
				si.RequireConfirmedEmail = true;
				si.RequireConfirmedAccount = true;
				si.RequireConfirmedPhoneNumber = false;

				var us = opts.User;
				us.RequireUniqueEmail = true;
			});
			*/

			services.AddWorkflowEngine();
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

// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading.Tasks;
using System.Collections.Generic;
using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Module.Infrastructure;
using A2v10.ReportEngine.Pdf;
using A2v10.Workflow.Engine;
using A2v10.Scheduling;
using A2v10.Scheduling.Commands;
using A2v10.Core.Web.Site.TestServices;

namespace A2v10.Core.Web.Site;

public class NullLicenseManager : ILicenseManager
{
    public Task<bool> VerifyLicensesAsync(string? dataSource, int? tenantId, IEnumerable<Guid> modules)
    {
        return Task.FromResult(true);
    }
}

public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	public void ConfigureServices(IServiceCollection services)
	{
		//!!!Before UsePlatform(). It has a default implementation.
		services.UseMailClient();
		services.AddScoped<IUserBannerProvider, TestBannerProvider>();
		//services.UseLicenseManager();
		services.AddScoped<ILicenseManager, NullLicenseManager>();

		services.AddScoped<ISqlQueryTextProvider, SqlQueryTextProvider>();

		services.UsePlatform(Configuration);

		services.AddSingleton<TestBusinessAppProvider>();

		services.AddReportEngines(factory =>
		{
			factory.RegisterEngine<PdfReportEngine>("pdf");
		});


		services.AddWorkflowEngineScoped()
		.AddInvokeTargets(a =>
		{
			a.RegisterEngine<WorkflowInvokeTarget>("Workflow", InvokeScope.Scoped);
		});

		services.UseScheduling(Configuration, factory =>
		{
			// job handlers
			factory.RegisterJobHandler<ExecuteSqlJobHandler>("ExecuteSql")
            .RegisterJobHandler<ProcessCommandsJobHandler>("ProcessCommands")
            .RegisterJobHandler<WorkflowPendingJobHandler>("WorkflowPending");
            // commands
            factory.RegisterCommand<ScheduledSendMailCommand>("SendMail")
            .RegisterCommand<ScheduledExecuteSqlCommand>("ExecuteSql");
        });
    }

	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		app.ConfigurePlatform(env);
	}
}

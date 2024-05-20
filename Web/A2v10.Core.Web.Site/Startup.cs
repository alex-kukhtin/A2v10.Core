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
using A2v10.AppRuntimeBuilder;
using A2v10.Identity.Core;
using A2v10.Core.Web.Site.TestServices;

using A2v10.BlobStorage.Azure;

namespace A2v10.Core.Web.Site;

public class NullLicenseManager : ILicenseManager
{
    public Task<bool> VerifyLicensesAsync(string? dataSource, int? tenantId, IEnumerable<Guid> modules)
    {
        return Task.FromResult(true);
    }
}

public class Startup(IConfiguration configuration)
{
    public IConfiguration Configuration { get; } = configuration;

    public void ConfigureServices(IServiceCollection services)
	{
		//!!!Before UsePlatform(). It has a default implementation.
		//services.UseMailClient();
		services.UseAppRuntimeBuilder();
		//services.UsePermissions();
		//services.AddScoped<IUserBannerProvider, TestBannerProvider>();
		//services.UseLicenseManager();
		services.AddScoped<ILicenseManager, NullLicenseManager>();

		services.AddScoped<ISqlQueryTextProvider, SqlQueryTextProvider>();

		var builders = services.UsePlatform(Configuration);

		/*
		builders.AuthenticationBuilder.AddGoogle(opts =>
		{
			opts.ClientId = Configuration.GetValue<String>("Identity:Google:ClientId")
				?? throw new InvalidOperationException("Identity:Google:ClientId not found");
			opts.ClientSecret = Configuration.GetValue<String>("Identity:Google:ClientSecret")
				?? throw new InvalidOperationException("Identity:Google:ClientSecret not found");
			opts.Events.OnRemoteFailure = OpenIdErrorHandlers.OnRemoteFailure;
		});
		.AddMicrosoftAccount(opts =>
		{
			opts.ClientId = Configuration.GetValue<String>("Identity:Microsoft:ClientId")
				?? throw new InvalidOperationException("Identity:Microsoft:ClientId not found");
			opts.ClientSecret = Configuration.GetValue<String>("Identity:Microsoft:ClientSecret")
				?? throw new InvalidOperationException("Identity:Microsoft:ClientSecret not found");
			opts.Events.OnRemoteFailure = OpenIdErrorHandlers.OnRemoteFailure;
		});
		*/

		services.AddSingleton<TestBusinessAppProvider>();

		services.AddReportEngines(factory =>
		{
			factory.RegisterEngine<PdfReportEngine>("pdf");
		});

		services.AddBlobStorages(factory =>
		{
			factory.RegisterStorage<AzureBlobStorage>("AzureStorage");
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

			factory.RegisterJobHandler<ExecuteSqlJobHandler>(); // with type name
            // commands
            factory.RegisterCommand<ScheduledSendMailCommand>("SendMail")
            .RegisterCommand<ScheduledExecuteSqlCommand>("ExecuteSql");
        });
	
		services.AddKeyedScoped<IEndpointHandler, TestPageHandler>("SqlReports");
		services.AddKeyedScoped<IEndpointHandler, TestPageHandler>("MyData");
	}

	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		app.ConfigurePlatform(env, Configuration);
	}
}

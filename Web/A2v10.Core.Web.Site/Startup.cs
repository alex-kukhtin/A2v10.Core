// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

using A2v10.WorkflowEngine;
using A2v10.ReportEngine.Pdf;

namespace A2v10.Core.Web.Site;

public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	public void ConfigureServices(IServiceCollection services)
	{
		//!!!Before Use Platform. It has a default implementation 
		services.UseMailClient();
		services.UseLicenseManager();

		services.UsePlatform(Configuration);

		services.UseSimpleIdentityOptions();

		services.AddReportEngines(factory =>
		{
			factory.RegisterEngine<PdfReportEngine>("pdf");
		});


		services.AddWorkflowEngineScoped();
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

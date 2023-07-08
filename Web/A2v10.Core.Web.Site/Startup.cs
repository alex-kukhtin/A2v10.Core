// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

using A2v10.WorkflowEngine;
using A2v10.ReportEngine.Pdf;
using A2v10.Module.Infrastructure;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

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
		//!!!Before Use Platform. It has a default implementation 
		services.UseMailClient();
		//services.UseLicenseManager();
		services.AddScoped<ILicenseManager, NullLicenseManager>();

		services.UsePlatform(Configuration);

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

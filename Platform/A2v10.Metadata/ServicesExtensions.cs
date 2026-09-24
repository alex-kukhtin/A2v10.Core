// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;
using A2v10.App.Infrastructure;
using A2v10.Metadata;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
    public static IServiceCollection UseAppMetadata(this IServiceCollection services)
    {

        services.AddSingleton<DatabaseMetadataCache>()
            .AddScoped<SqlDbGenerator>()
            .AddScoped<DatabaseMetadataProvider>()
            .AddScoped<IAppRuntimeBuilder, AppMetadataBuilder>();

        services.AddScoped<IModelBuilderFactory, ModelBuilderFactory>();
        services.AddScoped<IEndpointGenerator, EndpointGenerator>();
        services.AddScoped<EndpointMaterializer>();
        services.AddScoped<EndpointValidator>();

        services.AddScoped<ILicenseManager, LicenseManager>();  

        services.AddKeyedScoped<IModelReportHandler, PrintReportHandler>(":Metadata.Report");

        return services;
    }

    /* The test host only: the database a run owns and the scenarios. The scenarios open endpoints
     * through EndpointDataService, so it comes along; who the user is stays the harness's to say.
     */
    public static IServiceCollection UseTestEnvironment(this IServiceCollection services)
    {
        services.UseEndpointDataServices()
            .AddScoped<TestEnvironment>();
        return services;
    }

    public static IServiceCollection UseApplicationClr(this IServiceCollection services,
        Action<AppMetadataClrOptions> action)
    {
        var options = new AppMetadataClrOptions();
        action(options);
        services.AddScoped<IAppClrProvider>(sp => new AppMetadataClrProvider(options, sp))
            .AddScoped<IAppClrManager, AppMetadataClrManager>();
        return services;
    }
}

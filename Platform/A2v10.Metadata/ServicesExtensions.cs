// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

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
            .AddScoped<DatabaseMetadataProvider>()
            .AddScoped<IAppRuntimeBuilder, AppMetadataBuilder>();

        services.AddKeyedScoped<IEndpointHandler, MetadataEndpointHandler>("Meta");

        services.AddScoped<IModelBuilderFactory, ModelBuilderFactory>();
        services.AddScoped<IEndpointGenerator, EndpointGenerator>();

        services.AddScoped<ILicenseManager, LicenseManager>();  

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

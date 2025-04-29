// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Metadata;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
    public static IServiceCollection UseAppMetdata(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseMetadataCache>()
            .AddScoped<DatabaseMetadataProvider>()
            .AddScoped<IAppRuntimeBuilder, AppMetadataBuilder>();

        services.AddKeyedScoped<IEndpointHandler, MetadataEndpointHandler>("Meta");

        services.AddScoped<IModelBuilderFactory, ModelBuilderFactory>();

        return services;
    }
}

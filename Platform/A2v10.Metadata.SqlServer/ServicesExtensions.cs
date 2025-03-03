// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;

using A2v10.Metadata.SqlServer;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
    public static IServiceCollection UseAppMetdataBuilder(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseMetadataCache>()
            .AddScoped<DatabaseMetadataProvider>()
            .AddScoped<IAppRuntimeBuilder, AppMetadataBuilder>();
        return services;
    }
}

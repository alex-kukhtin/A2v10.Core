// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.


using A2v10.Infrastructure;
using A2v10.AppRuntimeBuilder;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
	public static IServiceCollection UseAppRuntimeBuilder(this IServiceCollection services)
	{
		services.AddSingleton<RuntimeMetadataProvider>()
			.AddScoped<IAppRuntimeBuilder, AppRuntimeBuilder>();
		return services;
	}
}

// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

public static class ServicesExtensions
{
	public static IServiceCollection UseAppRuntimeBuilder(this IServiceCollection services)
	{
		services.AddSingleton<RuntimeMetadataProvider>()
			.AddScoped<IAppRuntimeBuilder, AppRuntimeBuilder>();
		return services;
	}
}

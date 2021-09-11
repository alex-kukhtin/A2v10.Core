// Copyright © 2021 Alex Kukhtin. All rights reserved.


using A2v10.Data;
using A2v10.Data.Interfaces;

namespace Microsoft.Extensions.DependencyInjection
{
	public static class ServiceExtensions
	{
		public static IServiceCollection AddSqlServerStorage(this IServiceCollection services)
		{
			// Storage
			services.AddOptions<DataConfigurationOptions>();
			
			services.AddScoped<IDbContext, SqlDbContext>()
			.AddScoped<IDataConfiguration, DataConfiguration>();

			services.Configure<DataConfigurationOptions>(opts =>
			{
				opts.ConnectionStringName = "Default";
			});
			return services;
		}

		public static IServiceCollection AddSqlServerDefaultImpl(this IServiceCollection services)
		{
			services.AddSingleton<IDataProfiler, NullDataProfiler>()
				.AddSingleton<IDataLocalizer, NullDataLocalizer>();
			return services;
		}
	}
}

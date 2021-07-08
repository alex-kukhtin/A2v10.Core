// Copyright © 2021 Alex Kukhtin. All rights reserved.

using Microsoft.Extensions.Configuration;

using A2v10.Data;
using A2v10.Data.Config;
using A2v10.Data.Interfaces;

namespace Microsoft.Extensions.DependencyInjection
{
	public static class ServiceExtensions
	{
		public static IServiceCollection AddSqlServerStorage(this IServiceCollection services)
		{
			// Storage
			services.AddScoped<IDbContext>(s =>
				new SqlDbContext(
					s.GetService<IDataProfiler>(),
					new DataConfiguration(s.GetService<IConfiguration>(), opts =>
					{
						opts.ConnectionStringName = "Default";
					}),
					s.GetService<IDataLocalizer>(),
					s.GetService<ITenantManager>(),
					s.GetService<ITokenProvider>()
				)
			);
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

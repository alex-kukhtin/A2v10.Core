// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.


using A2v10.Infrastructure;
using A2v10.MailClient;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
	public static IServiceCollection UseMailClient(this IServiceCollection services)
	{
		services.AddSingleton<IMailService, MailClient>();
		return services;
	}
}

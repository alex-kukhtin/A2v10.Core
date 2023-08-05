// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.


using Microsoft.AspNetCore.Http;

using A2v10.Identity.UI;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
	public static IMvcBuilder AddDefaultIdentityUI(this IMvcBuilder builder)
	{
		var assembly = typeof(AccountController).Assembly;
		builder.AddApplicationPart(assembly);
		builder.Services.AddAntiforgery(opts =>
		{
			opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
		});
		return builder;
	}
}

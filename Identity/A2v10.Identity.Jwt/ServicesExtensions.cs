// Copyright © 2021 Alex Kukhtin. All rights reserved.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

using A2v10.Identity.Jwt;

namespace Microsoft.Extensions.DependencyInjection;
public static class ServicesExtensions
{
	public static AuthenticationBuilder AddPlatformJwtBearer(this AuthenticationBuilder builder, IConfiguration configuration)
	{
		var settings = JwtBearerSettings.Create(configuration);

		builder.Services.AddSingleton<JwtBearerSettings>(settings)
		.AddSingleton<JwtBearerService>();

		builder.AddJwtBearer(opts =>
			{
				settings.SetOptions(opts);
			});
		return builder;
	}
}


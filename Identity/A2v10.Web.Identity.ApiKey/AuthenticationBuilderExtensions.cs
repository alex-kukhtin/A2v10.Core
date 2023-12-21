// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Web.Identity.ApiKey;

public static class AuthenticationBuilderExtensions
{
	public static AuthenticationBuilder AddApiKeyAuthorization<T>(this AuthenticationBuilder authenticationBuilder, Action<ApiKeyAuthenticationOptions>? options = null)
		where T : struct
	{
		authenticationBuilder.Services.AddOptions<ApiKeyConfigurationOptions>();
		return authenticationBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler<T>>(ApiKeyAuthenticationOptions.DefaultScheme, options);
	}
}

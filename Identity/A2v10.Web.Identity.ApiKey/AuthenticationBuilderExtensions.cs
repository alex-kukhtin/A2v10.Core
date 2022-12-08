// Copyright © 2020-2022 Alex Kukhtin. All rights reserved.

using System;
using Microsoft.AspNetCore.Authentication;

namespace A2v10.Web.Identity.ApiKey;

public static class AuthenticationBuilderExtensions
{
	public static AuthenticationBuilder AddApiKeyAuthorization<T>(this AuthenticationBuilder authenticationBuilder, Action<ApiKeyAuthenticationOptions>? options = null)
		where T : struct
	{
		return authenticationBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler<T>>(ApiKeyAuthenticationOptions.DefaultScheme, options);
	}
}

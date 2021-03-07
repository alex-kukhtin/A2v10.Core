
using System;
using Microsoft.AspNetCore.Authentication;

namespace A2v10.Web.Identity.ApiKey
{
	public static class AuthenticationBuilderExtensions
	{
		public static AuthenticationBuilder AddApiKeyAuthorization(this AuthenticationBuilder authenticationBuilder, Action<ApiKeyAuthenticationOptions> options = null)
		{
			return authenticationBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, options);
		}
	}
}

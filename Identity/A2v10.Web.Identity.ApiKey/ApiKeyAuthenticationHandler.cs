// Copyright © 2020-2023 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// based on: https://josef.codes/asp-net-core-protect-your-api-with-api-keys/

namespace A2v10.Web.Identity.ApiKey;

public class ApiKeyAuthenticationHandler<T> : AuthenticationHandler<ApiKeyAuthenticationOptions>
		where T:struct
{
	private readonly IUserLoginStore<AppUser<T>> _userLoginStore;
	private readonly IUserClaimStore<AppUser<T>> _userClaimStore;
	public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, 
			IUserLoginStore<AppUser<T>> userStore, IUserClaimStore<AppUser<T>> claimStore)
		: base(options, logger, encoder, clock)
	{
		_userLoginStore = userStore;
		_userClaimStore = claimStore;
	}

	protected async override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var apiKeyHeaderValues))
			return AuthenticateResult.NoResult();
		if (apiKeyHeaderValues.Count == 0)
			return AuthenticateResult.NoResult();
		var apiKey = apiKeyHeaderValues.FirstOrDefault();
		if (String.IsNullOrEmpty(apiKey))
			return AuthenticateResult.NoResult();
		var appUser = await _userLoginStore.FindByLoginAsync("ApiKey", apiKey, CancellationToken.None);
		if (appUser == null || appUser.IsEmpty)
			return AuthenticateResult.NoResult();
		var claims = await _userClaimStore.GetClaimsAsync(appUser, CancellationToken.None);

		var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.AuthenticationType);
		var identities = new List<ClaimsIdentity> { identity };
		var principal = new ClaimsPrincipal(identities);
		var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.DefaultScheme);

		Response.Headers.Add("WWW-Authenticate", apiKey);
		return AuthenticateResult.Success(ticket);	
	}

	protected override Task HandleChallengeAsync(AuthenticationProperties properties)
	{
		return base.HandleChallengeAsync(properties);
	}

	protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
	{
		return base.HandleForbiddenAsync(properties);
	}
}

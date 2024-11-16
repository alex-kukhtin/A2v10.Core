// Copyright © 2020-2023 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using A2v10.Identity.Core;

// based on: https://josef.codes/asp-net-core-protect-your-api-with-api-keys/

namespace A2v10.Web.Identity.ApiKey;

public class ApiKeyAuthenticationHandler<T>(IOptionsMonitor<ApiKeyAuthenticationOptions> _options, ILoggerFactory _logger, UrlEncoder _encoder,
    IUserLoginStore<AppUser<T>> _userStore, IUserClaimStore<AppUser<T>> _claimStore, IOptions<ApiKeyConfigurationOptions> _configOptions) 
		: AuthenticationHandler<ApiKeyAuthenticationOptions>(_options, _logger, _encoder)
		where T : struct
{
    private readonly ApiKeyConfigurationOptions _configOptions = _configOptions.Value;

	private const String ProviderName = "ApiKey";

    protected async override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var apiKeyHeaderValues))
			return AuthenticateResult.NoResult();
		if (apiKeyHeaderValues.Count == 0)
			return AuthenticateResult.NoResult();
		var apiKey = apiKeyHeaderValues.FirstOrDefault();
		if (String.IsNullOrEmpty(apiKey))
			return AuthenticateResult.NoResult();

		var appUser = _configOptions.KeyType switch
		{
			KeyType.ApiKey => await _userStore.FindByLoginAsync(ProviderName, apiKey, CancellationToken.None),
			KeyType.EncodedClaims => await GetApiUserFromClaimsAsync(apiKey),
			_ => throw new InvalidOperationException("Yet not implemented")
		};
		if (appUser == null || appUser.IsEmpty)
			return AuthenticateResult.NoResult();
		var claims = await _claimStore.GetClaimsAsync(appUser, CancellationToken.None);
		// ID is required
		claims.Add(new Claim(WellKnownClaims.NameIdentifier, appUser.Id.ToString()!));

		var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.AuthenticationType);
		var identities = new List<ClaimsIdentity> { identity };
		var principal = new ClaimsPrincipal(identities);
		var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.DefaultScheme);

		Response.Headers.Append("WWW-Authenticate", apiKey);
		return AuthenticateResult.Success(ticket);
	}

	private async Task<AppUser<T>?> GetApiUserFromClaimsAsync(String apiKey)
	{
		var user = ApiKeyUserHelper<T>.GetUserFromApiKey(apiKey, _configOptions.AesEncryptKey, _configOptions.AesEncryptVector);
		if (user == null)
			return null;
		if (_configOptions.SkipCheckUser)
			return user;
		var userFromDb = await _userStore.FindByLoginAsync(ProviderName, apiKey, CancellationToken.None);
		if (userFromDb == null)
			return null;
		if (!Equals(userFromDb.Id, user.Id))
			return null;	
        return user;
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


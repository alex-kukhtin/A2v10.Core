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

public class ApiKeyAuthenticationHandler<T> : AuthenticationHandler<ApiKeyAuthenticationOptions>
		where T : struct
{
	private readonly IUserLoginStore<AppUser<T>> _userLoginStore;
	private readonly IUserClaimStore<AppUser<T>> _userClaimStore;
	private readonly ApiKeyConfigurationOptions _configOptions;

	private const String ProviderName = "ApiKey";

#if NET8_0_OR_GREATER
#pragma warning disable IDE0290 // Use primary constructor
    public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder,
#pragma warning restore IDE0290 // Use primary constructor
        IUserLoginStore<AppUser<T>> userStore, IUserClaimStore<AppUser<T>> claimStore, IOptions<ApiKeyConfigurationOptions> configOptions)
        : base(options, logger, encoder)
    {
        _userLoginStore = userStore;
        _userClaimStore = claimStore;
		_configOptions = configOptions.Value;
    }
#else
#pragma warning disable IDE0290 // Use primary constructor
	public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock,
#pragma warning restore IDE0290 // Use primary constructor
		IUserLoginStore<AppUser<T>> userStore, IUserClaimStore<AppUser<T>> claimStore, IOptions<ApiKeyConfigurationOptions> configOptions)
		: base(options, logger, encoder, clock)
	{
		_userLoginStore = userStore;
		_userClaimStore = claimStore;
		_configOptions = configOptions.Value;
	}
#endif

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
			KeyType.ApiKey => await _userLoginStore.FindByLoginAsync(ProviderName, apiKey, CancellationToken.None),
			KeyType.EncodedClaims => await GetApiUserFromClaims(apiKey),
			_ => throw new InvalidOperationException("Yet not implemented")
		};
		if (appUser == null || appUser.IsEmpty)
			return AuthenticateResult.NoResult();
		var claims = await _userClaimStore.GetClaimsAsync(appUser, CancellationToken.None);
		// ID is required
		claims.Add(new Claim(WellKnownClaims.NameIdentifier, appUser.Id.ToString()!));

		var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.AuthenticationType);
		var identities = new List<ClaimsIdentity> { identity };
		var principal = new ClaimsPrincipal(identities);
		var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.DefaultScheme);

		Response.Headers.Append("WWW-Authenticate", apiKey);
		return AuthenticateResult.Success(ticket);
	}

	private async Task<AppUser<T>?> GetApiUserFromClaims(String apiKey)
	{
		var user = ApiKeyUserHelper<T>.GetUserFromApiKey(apiKey, _configOptions.AesEncryptKey, _configOptions.AesEncryptVector);
		if (user == null)
			return null;
		if (_configOptions.SkipCheckUser)
			return user;
		var userFromDb = await _userLoginStore.FindByLoginAsync(ProviderName, apiKey, CancellationToken.None);
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


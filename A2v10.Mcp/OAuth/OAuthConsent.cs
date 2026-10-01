// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.WebUtilities;

using A2v10.Web.Identity;

namespace A2v10.Mcp.OAuth;

public sealed class OAuthConsent(OAuthProtector protector, AppUserStore<Int64> userStore) : IOAuthConsent
{
	public const String CodeProvider = "mcp-code";
	static readonly TimeSpan CodeLifetime = TimeSpan.FromSeconds(60);

	public OAuthConsentInfo? Read(String request)
	{
		var r = protector.ReadRequest(request);
		if (r == null)
			return null;
		var uri = new Uri(r.RedirectUri);
		return new OAuthConsentInfo(r.ClientName, uri.Host, uri.IsLoopback);
	}

	public async Task<String?> ApproveAsync(String request, ClaimsPrincipal user)
	{
		var r = protector.ReadRequest(request);
		if (r == null || user.Identity?.IsAuthenticated != true)
			return null;
		var code = new AuthorizationCode(user.Identity.GetUserId<Int64>(), user.Identity.GetUserTenant<Int64>(),
			r.ClientKey, r.RedirectUri, r.Challenge, r.Scope, OAuthProtector.NewSecret());
		await userStore.AddTokenAsync(new AppUser<Int64>() { Id = code.UserId, Tenant = code.Tenant },
			CodeProvider, code.Nonce, DateTime.UtcNow + CodeLifetime);
		return WithQuery(r.RedirectUri, ("code", protector.ProtectCode(code, CodeLifetime)), ("state", r.State));
	}

	public String? Deny(String request)
	{
		var r = protector.ReadRequest(request);
		return r == null ? null : RedirectError(r.RedirectUri, r.State, "access_denied");
	}

	public static String RedirectError(String redirectUri, String? state, String error, String? description = null) =>
		WithQuery(redirectUri, ("error", error), ("error_description", description), ("state", state));

	static String WithQuery(String uri, params (String Key, String? Value)[] items) =>
		QueryHelpers.AddQueryString(uri, items.Where(i => i.Value != null).Select(i => KeyValuePair.Create(i.Key, i.Value)));
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace A2v10.Mcp.OAuth;

/* DCR without state: the client_id is the registration itself, encrypted. */
public record OAuthClient(String[] RedirectUris, String? Name)
{
	public static Boolean IsAllowedRedirect(String uri) =>
		Uri.TryCreate(uri, UriKind.Absolute, out var u)
		&& (u.Scheme == Uri.UriSchemeHttps || (u.Scheme == Uri.UriSchemeHttp && u.IsLoopback));

	// An unknown redirect is shown, never redirected to: the redirect target is what is in doubt.
	public Boolean Allows(String? redirectUri) =>
		redirectUri != null && RedirectUris.Any(r => RedirectMatches(r, redirectUri));

	/* Claude Code listens on an ephemeral port that changes per session while its registration names
	 * none (RFC 8252 7.3), so a loopback redirect matches with the port ignored; anything else - exactly.
	 */
	static Boolean RedirectMatches(String registered, String requested)
	{
		if (registered == requested)
			return true;
		if (!Uri.TryCreate(registered, UriKind.Absolute, out var r) || !Uri.TryCreate(requested, UriKind.Absolute, out var q))
			return false;
		return r.IsLoopback && q.IsLoopback && r.Scheme == q.Scheme && r.Host == q.Host && r.PathAndQuery == q.PathAndQuery;
	}
}

// ClientKey instead of the client_id: the request travels in URLs, through the login's returnurl too.
public record AuthorizationRequest(String ClientKey, String? ClientName, String RedirectUri, String? State, String Challenge, String Scope);

// Nonce is the row in RefreshTokens that makes the code single-use.
public record AuthorizationCode(Int64 UserId, Int64? Tenant, String ClientKey, String RedirectUri, String Challenge, String Scope, String Nonce);

// Nonce is the row "mcp:<ClientKey>" in RefreshTokens; a new one on every rotation.
public record RefreshGrant(Int64 UserId, Int64? Tenant, String ClientKey, String Scope, String Nonce)
{
	public String Provider => $"mcp:{ClientKey}";
}

public sealed class OAuthProtector(IDataProtectionProvider provider)
{
	readonly IDataProtector _client = provider.CreateProtector("A2v10.Mcp.OAuth.Client");
	readonly ITimeLimitedDataProtector _request = provider.CreateProtector("A2v10.Mcp.OAuth.Request").ToTimeLimitedDataProtector();
	readonly ITimeLimitedDataProtector _code = provider.CreateProtector("A2v10.Mcp.OAuth.Code").ToTimeLimitedDataProtector();
	readonly ITimeLimitedDataProtector _refresh = provider.CreateProtector("A2v10.Mcp.OAuth.Refresh").ToTimeLimitedDataProtector();

	static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(10);

	public String ProtectClient(OAuthClient client) => _client.Protect(JsonSerializer.Serialize(client));
	public OAuthClient? ReadClient(String? clientId) => Read<OAuthClient>(_client, clientId);

	public String ProtectRequest(AuthorizationRequest request) => _request.Protect(JsonSerializer.Serialize(request), RequestLifetime);
	public AuthorizationRequest? ReadRequest(String? request) => Read<AuthorizationRequest>(_request, request);

	public String ProtectCode(AuthorizationCode code, TimeSpan lifetime) => _code.Protect(JsonSerializer.Serialize(code), lifetime);
	public AuthorizationCode? ReadCode(String? code) => Read<AuthorizationCode>(_code, code);

	public String ProtectRefresh(RefreshGrant grant, TimeSpan lifetime) => _refresh.Protect(JsonSerializer.Serialize(grant), lifetime);
	public RefreshGrant? ReadRefresh(String? token) => Read<RefreshGrant>(_refresh, token);

	// Short and fixed-length: fits RefreshTokens.Provider as "mcp:<key>".
	public static String ClientKey(String clientId) =>
		WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(clientId)))[..22];

	public static String NewSecret() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

	// Not ours, tampered or expired read alike: a caller only needs "not valid".
	static T? Read<T>(IDataProtector protector, String? text) where T : class
	{
		if (String.IsNullOrEmpty(text))
			return null;
		try
		{
			return JsonSerializer.Deserialize<T>(protector.Unprotect(text));
		}
		catch (CryptographicException)
		{
			return null;
		}
	}
}

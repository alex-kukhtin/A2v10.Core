// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

using A2v10.Web.Identity;

namespace A2v10.Mcp.OAuth;

/* The authorization server Claude talks to: RFC 8414 metadata, DCR (RFC 7591), authorization code
 * with S256 PKCE, refresh rotated on every use.
 */
public class OAuthController(McpAddress address, OAuthProtector protector, AppUserStore<Int64> userStore,
	UserManager<AppUser<Int64>> userManager, SignInManager<AppUser<Int64>> signInManager,
	IOptionsMonitor<BearerTokenOptions> bearerOptions) : Controller
{
	public const String AccessTokenScheme = "A2v10.Mcp";
	public const String Scope = "mcp:tools";
	public const String OfflineAccess = "offline_access";

	const String Prefix = "/oauth";

	static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(10);
	static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

	[HttpGet("/.well-known/oauth-authorization-server")]
	public IActionResult Metadata() => Json(new
	{
		issuer = address.Issuer,
		authorization_endpoint = $"{address.Issuer}{Prefix}/authorize",
		token_endpoint = $"{address.Issuer}{Prefix}/token",
		registration_endpoint = $"{address.Issuer}{Prefix}/register",
		response_types_supported = new[] { "code" },
		grant_types_supported = new[] { "authorization_code", "refresh_token" },
		code_challenge_methods_supported = new[] { "S256" },
		token_endpoint_auth_methods_supported = new[] { "none" },
		scopes_supported = new[] { Scope, OfflineAccess },
	});

	public record RegistrationRequest(
		[property: JsonPropertyName("redirect_uris")] String[]? RedirectUris,
		[property: JsonPropertyName("client_name")] String? ClientName);

	// Read by hand: the host's MVC input formatter may be Newtonsoft, which ignores JsonPropertyName.
	[HttpPost(Prefix + "/register")]
	public async Task<IActionResult> Register()
	{
		RegistrationRequest? request;
		try
		{
			request = await JsonSerializer.DeserializeAsync<RegistrationRequest>(Request.Body);
		}
		catch (JsonException)
		{
			return Error("invalid_client_metadata", "the body is not JSON");
		}
		var uris = request?.RedirectUris ?? [];
		if (uris.Length == 0 || !uris.All(OAuthClient.IsAllowedRedirect))
			return Error("invalid_redirect_uri", "redirect_uris must be https or http loopback");
		var client = new OAuthClient(uris, request?.ClientName);
		return new JsonResult(new
		{
			client_id = protector.ProtectClient(client),
			client_id_issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			client_name = client.Name,
			redirect_uris = client.RedirectUris,
			grant_types = new[] { "authorization_code", "refresh_token" },
			response_types = new[] { "code" },
			token_endpoint_auth_method = "none",
		})
		{ StatusCode = StatusCodes.Status201Created };
	}

	/* Checks the request, then sends the browser to the cookie login and back here, then to the
	 * consent screen with the checked request as one encrypted parameter.
	 */
	[HttpGet(Prefix + "/authorize")]
	public async Task<IActionResult> Authorize()
	{
		var q = Request.Query;
		String? clientId = q["client_id"];
		String? redirectUri = q["redirect_uri"];
		var client = protector.ReadClient(clientId);
		if (client == null || !client.Allows(redirectUri))
			return BadRequest("Unknown client or redirect_uri: the request did not come from a registered client.");

		String? state = q["state"];
		if (q["response_type"] != "code")
			return Redirect(OAuthConsent.RedirectError(redirectUri!, state, "unsupported_response_type"));
		String? challenge = q["code_challenge"];
		if (String.IsNullOrEmpty(challenge) || q["code_challenge_method"] != "S256")
			return Redirect(OAuthConsent.RedirectError(redirectUri!, state, "invalid_request", "S256 PKCE is required"));
		String? resource = q["resource"];
		if (!String.IsNullOrEmpty(resource) && resource.TrimEnd('/') != address.Resource)
			return Redirect(OAuthConsent.RedirectError(redirectUri!, state, "invalid_target"));

		var login = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
		if (!login.Succeeded)
			return Challenge(new AuthenticationProperties() { RedirectUri = Request.Path + Request.QueryString },
				IdentityConstants.ApplicationScheme);

		var request = new AuthorizationRequest(OAuthProtector.ClientKey(clientId!), client.Name, redirectUri!, state,
			challenge, NormalizeScope(q["scope"]));
		return Redirect($"/account/consent?request={Uri.EscapeDataString(protector.ProtectRequest(request))}");
	}

	[HttpPost(Prefix + "/token")]
	public async Task<IActionResult> Token()
	{
		// RFC 6749 5.1: token responses, errors included, are not cached.
		Response.Headers.CacheControl = "no-store";
		if (!Request.HasFormContentType)
			return Error("invalid_request", "application/x-www-form-urlencoded is required");
		var f = await Request.ReadFormAsync();
		String? clientId = f["client_id"];
		return f["grant_type"].ToString() switch
		{
			"authorization_code" => await ExchangeCode(clientId, f["code"], f["redirect_uri"], f["code_verifier"]),
			"refresh_token" => await ExchangeRefresh(clientId, f["refresh_token"]),
			_ => Error("unsupported_grant_type"),
		};
	}

	// Spent before it is checked: a code is used up by the first attempt, right or wrong.
	async Task<IActionResult> ExchangeCode(String? clientId, String? code, String? redirectUri, String? verifier)
	{
		var c = protector.ReadCode(code);
		if (c == null || !await userStore.RotateTokenAsync(Owner(c.UserId, c.Tenant), OAuthConsent.CodeProvider, c.Nonce))
			return Error("invalid_grant", "unknown, expired or used code");
		if (clientId == null || OAuthProtector.ClientKey(clientId) != c.ClientKey || c.RedirectUri != redirectUri)
			return Error("invalid_grant", "client_id or redirect_uri differ from the authorization request");
		if (verifier == null || WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))) != c.Challenge)
			return Error("invalid_grant", "PKCE verification failed");
		var grant = new RefreshGrant(c.UserId, c.Tenant, c.ClientKey, c.Scope, OAuthProtector.NewSecret());
		await userStore.AddTokenAsync(Owner(grant.UserId, grant.Tenant), grant.Provider, grant.Nonce, DateTime.UtcNow + RefreshLifetime);
		return await Issue(grant);
	}

	// Rotation: the old token dies in the same call that stores the new one.
	async Task<IActionResult> ExchangeRefresh(String? clientId, String? token)
	{
		var r = protector.ReadRefresh(token);
		if (r == null || clientId == null || OAuthProtector.ClientKey(clientId) != r.ClientKey)
			return Error("invalid_grant");
		var grant = r with { Nonce = OAuthProtector.NewSecret() };
		if (!await userStore.RotateTokenAsync(Owner(r.UserId, r.Tenant), r.Provider, r.Nonce, grant.Nonce, DateTime.UtcNow + RefreshLifetime))
			return Error("invalid_grant", "unknown or used refresh token");
		return await Issue(grant);
	}

	/* The user is read again on every issue, so a lockout or changed roles take effect at the next
	 * refresh. The principal comes from the same factory as the login cookie's; the ticket is sealed
	 * with the MCP scheme's own protector, so no other scheme of the host can read it.
	 */
	async Task<IActionResult> Issue(RefreshGrant grant)
	{
		var user = await userManager.FindByIdAsync(grant.UserId.ToString());
		if (user == null || await userManager.IsLockedOutAsync(user))
			return Error("invalid_grant", "the user is not available");
		var principal = await signInManager.CreateUserPrincipalAsync(user);
		var now = DateTimeOffset.UtcNow;
		var ticket = new AuthenticationTicket(principal,
			new AuthenticationProperties() { IssuedUtc = now, ExpiresUtc = now + AccessLifetime }, AccessTokenScheme);
		return Json(new
		{
			access_token = bearerOptions.Get(AccessTokenScheme).BearerTokenProtector.Protect(ticket),
			token_type = "Bearer",
			expires_in = (Int64)AccessLifetime.TotalSeconds,
			refresh_token = protector.ProtectRefresh(grant, RefreshLifetime),
			scope = grant.Scope,
		});
	}

	static AppUser<Int64> Owner(Int64 userId, Int64? tenant) => new() { Id = userId, Tenant = tenant };

	// Only scopes this server knows; an empty request means the tools.
	static String NormalizeScope(String? requested)
	{
		var known = (requested ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Where(s => s is Scope or OfflineAccess).Distinct().ToList();
		if (!known.Contains(Scope))
			known.Insert(0, Scope);
		return String.Join(' ', known);
	}

	static JsonResult Error(String error, String? description = null) =>
		new(new { error, error_description = description }) { StatusCode = StatusCodes.Status400BadRequest };
}

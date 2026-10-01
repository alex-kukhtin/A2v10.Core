// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Security.Claims;

namespace A2v10.Web.Identity;

/* The consent screen's view of an OAuth authorization request. The request is opaque here: its
 * format, the code and PKCE belong to the authorization server that implements this, so the screen
 * shows what Read returns and redirects to what Approve or Deny return. The name and host come only
 * from the decrypted request - open query parameters would let any phishing link choose them.
 */
public record OAuthConsentInfo(String? ClientName, String RedirectHost, Boolean IsLoopback);

public interface IOAuthConsent
{
	// null - the request is damaged or expired
	OAuthConsentInfo? Read(String request);
	// the client's redirect with the code; null - the request is damaged or expired
	Task<String?> ApproveAsync(String request, ClaimsPrincipal user);
	// the client's redirect with access_denied; null - the request is damaged or expired
	String? Deny(String request);
}

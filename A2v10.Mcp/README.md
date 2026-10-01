# About
A2v10.Mcp is an MCP (Model Context Protocol) server for the A2v10 platform applications,
with its own OAuth authorization server.

The user connects their model (Claude and other MCP clients) to the application.
The model acts as that user: the same account, the same rights.


# How to use

```csharp
services.UsePlatform(Configuration);

services.UseMcp(Configuration);
```

That is the whole wiring: authentication schemes, the MCP server, OAuth endpoints and the `/mcp` route.


# appsettings.json configuration

```json
"Mcp": {
    "PublicUrl": "https://your.domain.com"
}
```

`PublicUrl` is the address users connect to, without `/mcp`: the OAuth issuer.
`PublicUrl/mcp` is the MCP endpoint.
Behind a proxy or a tunnel it is the public address, not the one Kestrel listens on.


# Requirements

* **Public HTTPS.** Claude (claude.ai, Desktop, mobile) connects from Anthropic's cloud.
* **Database.** The platform script with `a2security.RotateToken` (single-user databases).
  Codes and refresh tokens are kept in `a2security.RefreshTokens`, no new tables.
* **Identity UI.** The consent screen is `/account/consent` of the platform's Identity UI.
* **DataProtection keys** that survive a restart (the platform keeps them in the database) -
  otherwise every client has to sign in again.


# Connect Claude

Settings → Connectors → Add custom connector → `https://your.domain.com/mcp`.

Claude registers itself, opens the application's login page and then the consent screen.
After *Allow* the connection works until the user disconnects it.

To disconnect all of a user's MCP clients on the server side, delete their rows
with `Provider like 'mcp:%'` from `a2security.RefreshTokens`.


# Local development

Use a tunnel pointed at the **https** port, e.g. ngrok:

```
ngrok http https://localhost:5001 --url=https://<your-domain>.ngrok-free.dev
```

and set `Mcp:PublicUrl` to the tunnel address.


# Endpoints

* `/mcp` - MCP (streamable HTTP, stateless)
* `/.well-known/oauth-protected-resource/mcp` - protected resource metadata
* `/.well-known/oauth-authorization-server` - authorization server metadata
* `/oauth/register` - dynamic client registration
* `/oauth/authorize` - authorization code with S256 PKCE
* `/oauth/token` - token (authorization code, refresh token with rotation)


# Tools

* `whoami` - the user the connection is signed in as


# Related Packages

* [A2v10.Identity.Core](https://www.nuget.org/packages/A2v10.Identity.Core)
* [A2v10.Platform](https://www.nuget.org/packages/A2v10.Platform)


# Feedback

A2v10.Mcp is released as open source under the MIT license.
Bug reports and contributions are welcome at the GitHub repository.

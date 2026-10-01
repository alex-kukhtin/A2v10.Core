# A2v10.Mcp

MCP endpoint `/mcp` for the user's own model (Claude), plus the OAuth authorization server that signs it in. The model acts as the user: their rights, their author stamp. Design and the protocol requirements Claude adds on top of the spec: `a2v10-md-skill/concepts/mcp/README.md` (outside this repo).

## One call, two mechanisms

`services.UseMcp(configuration)` is the whole wiring. Services must be registered before `Build`, so the call goes on services, not on `app`.

- OAuth endpoints (`/.well-known/oauth-authorization-server`, `/oauth/register`, `/oauth/authorize`, `/oauth/token`) are an MVC controller added as an application part — the platform already calls `MapControllers`, and it has no global filters.
- `/mcp` is the SDK's transport, not a controller. An `IStartupFilter` adds a second `UseEndpoints` after the host's `Configure`; it joins the same route table, since the matcher is built from all data sources on the first request.
- `GET /mcp` is mapped to 405: stateless has no GET stream, and without it the platform's `[Authorize]` catch-all in `MainController` answers with a login redirect.
- `/.well-known/oauth-protected-resource/mcp` needs no route — the `AddMcp` scheme answers it during `UseAuthentication`.

## The public URL comes from configuration

`mcp:publicUrl` is the issuer; `+ /mcp` is the resource. Claude requires `resource` to equal what the user typed, which behind a proxy is not what Kestrel sees. Through ngrok pointed at the **https** port (`ngrok http https://localhost:5001 --url=...`) `Host` passes unchanged and the scheme is https, so the cookie login redirects to the public host without `UseForwardedHeaders`. `http 5000` would not work: `ConfigurePlatform` does `UseHttpsRedirection` to localhost.

## Access token: a ticket of the MCP scheme, not a JWT

The access token is an `AuthenticationTicket` sealed by the `BearerTokenProtector` of the `A2v10.Mcp` scheme (`AddBearerToken`, shared framework). Clients treat it as opaque.

- The protector's purpose is the audience: no other scheme of the host can unprotect it, and it cannot unprotect theirs.
- The principal is `SignInManager.CreateUserPrincipalAsync` — the same factory as the login cookie, `AppUserStoreOptions` claims included — so `ICurrentUser.Identity` assembles exactly as for the cookie.
- The user is read again on every issue (code and refresh): a lockout or changed roles apply at the next refresh, at most 10 minutes (access lifetime).
- The scheme's challenge is forwarded to the `AddMcp` scheme, which adds `resource_metadata` to the 401.

Rejected: `JwtBearerService.BuildToken`. Its claim list is its own (no roles, admin, person name, branch; ignores store claims); `JwtBearerSettings.Create` throws without `Authentication:JwtBearer`, which web hosts lack; the platform scheme does not validate audience or issuer, so an MCP JWT would pass into other JWT APIs of a host (`A2v10.ApiHost` uses that scheme), and closing that means changing `DefaultValidationParameters` in a published package. `A2v10.Identity.Jwt` is therefore not a dependency.

## Stateless transport: the tool sees the request's user

`WithHttpTransport(o => o.Stateless = true)`. A tool runs in the request's scope, where `CurrentUserMiddleware` (after `UseAuthorization`, which has set `User` from the MCP scheme) has set up `ICurrentUser`. In stateful mode the SDK runs tools in its own session scope and `ICurrentUser` would be empty. Verified by `whoami`.

## No new tables

Every OAuth artifact is self-contained, encrypted by DataProtection (one purpose each: client, request, code, refresh); the database holds only what makes a token single-use.

- `client_id` = the registration itself `{redirect_uris, name}` — DCR without state.
- The authorization request carries `ClientKey` (22 chars of base64url SHA-256 of `client_id`), not the `client_id`: it travels through URLs, the login's `returnurl` included.
- Code = `{UserId, Tenant, ClientKey, redirect, challenge, scope, nonce}`, 60 s; row `Provider = "mcp-code"`.
- Refresh = `{UserId, Tenant, ClientKey, scope, nonce}`, 30 days; row `Provider = "mcp:<ClientKey>"`.
- The row's `Token` is the nonce only. `Provider nvarchar(64)` and `Token nvarchar(255)` would not hold a `client_id` or a whole token.
- "Disconnect Claude" = delete the user's rows `Provider like 'mcp:%'`. A family is one DCR registration, i.e. one connection.

## RotateToken: the check is the delete

`a2security.RotateToken` + `AppUserStore.RotateTokenAsync`. `GetToken` + `AddToken` would let two parallel refreshes with one token both pass; a `delete` by key takes the lock, and the second call finds no row.

- With `@NewToken` (refresh): one row → the new one is inserted. No row → the token was spent or revoked, and the whole `@Provider` family of the user is deleted.
- Without `@NewToken` (code): spent, nothing more. The code is spent by its first attempt, right or wrong, before PKCE is checked.
- Expired rows of the same user and provider are deleted on every call — codes that were never exchanged. Other providers are untouched.

Cost accepted: a rotation answer lost in the network, or the client refreshing twice in parallel, kills the connection — the user reconnects. If Claude is seen racing itself, fall back to plain `invalid_grant` without the family delete.

Only in `a2v10_security_simple.sql`. The multi-tenant scripts have no `RotateToken`; `RotateTokenAsync` passes `Tenant` like its neighbours, so it will fail there until one is written.

## Consent: the screen knows nothing of OAuth

`IOAuthConsent` lives in Identity.Core (`Read` → client name, redirect host, loopback; `ApproveAsync` / `Deny` → the redirect). `A2v10.Mcp` implements and registers it; request format, code and PKCE live only there. Name and host are shown only from the decrypted request — open query parameters would let a phishing link choose them.

- The screen is `Consent` GET/POST in `AccountController`, not its own controller: title, theme and antiforgery token are private helpers there. `IOAuthConsent? _oauthConsent = null` in the constructor; without MCP → 404.
- A plain `<form>` POST answering 302 to the client. `v-pre` on the form: the client name comes from DCR, and Vue would evaluate a `{{ }}` in it.
- `/oauth/authorize` reaches the login by a cookie-scheme `Challenge`, and the login returns to it. `TwoFactor` lowercased the whole `returnUrl`, which broke `client_id` and `code_challenge`; now it lowercases only for the `/account` comparison.

## Tools: C# boxes behind one interface, registered in code (decided 2026-10-01, not built)

A tool is always a C# type: `IMcpTool` — `Name`, `Description`, `InputSchema`, `Roles`, `ExecuteAsync(JsonElement args, CancellationToken)`. How it reaches data is its own business, through constructor dependencies; no door is imposed on it.

- **Rejected: a tool per endpoint × verb, derived from `model.json`/`metadata.json`.** Classic `model.json` types no parameter, so a description needs a second file per action; ~30 places × 3–4 verbs is ~100 schemas, 30–50k tokens in `tools/list` before the first question; a raw `IDataModel` result carries every column and `$` member. A domain tool with a shaped result (rows, total, `Truncated`) costs ~450 tokens of description + schema.
- **Rejected: a generic `describe` → `call` pair.** Untyped for the model, and every place costs a load to be described.
- **Rejected: `mcp.json` (the concept's "menu for the model").** The registry is code; a second list of the same tools in a file drifts from the classes.
- **Registration is a builder inside `UseMcp`:** `services.UseMcp(Configuration, tools => tools.Add<T>().AddProvider<P>())`. A tool cannot be registered without MCP wired; `UseMcp` stays the whole wiring.
- **`AddProvider<P>`** — scoped `IMcpToolProvider` returning `IEnumerable<IMcpTool>`. **`Add<T>`** — scoped `T` wrapped in a one-tool provider; the usual case. The registry is `IEnumerable<IMcpToolProvider>` of the request scope; there is no registry class. One method for both is impossible: C# does not overload by constraint.
- **Metadata mode is one provider.** It creates its tools itself (`new` or `ActivatorUtilities.CreateInstance` with the instance's settings — which report): DI does not know them. Hence `Name`, `Description`, `InputSchema`, `Roles` are instance members, not registration arguments; the schema may also depend on the user (resolver's kinds).
- **Every request creates every tool.** Stateless: `tools/list` and the lookup in `tools/call` both enumerate the providers. So a constructor takes dependencies and nothing else — no database, no computation; the work is in `ExecuteAsync`. A provider reads cached declarations, never files.
- **`McpTool<TArgs>` is what an application writes.** The schema is derived from the args record (`[Description]` on its members), the input arrives deserialized: a hand-written schema says `agent` while the code reads `agentId`. Raw `IMcpTool` remains for a schema that varies per request.
- **Around the tool, the platform.** `tools/list` / `tools/call` are handlers (`WithListToolsHandler` / `WithCallToolHandler`), not static `WithTools<>` — the list depends on the user. They filter by `Roles`; a name collision throws naming both providers (caught at list, not at start — a provider returns anything); arguments are checked against `InputSchema` before `ExecuteAsync`, a violation goes back as `isError` naming the field; a tool the user may not see answers as an unknown one, so the difference does not reveal it exists. `whoami` is registered by `Add<>` like any other.
- **Not verified:** that the SDK's handlers run in the request scope in stateless mode. The first probe.

## Rights: roles from the ticket, on the tool (decided 2026-10-01, not built)

`IMcpTool.Roles` is an abstract `String[]?`: `null`, written explicitly, is everyone; otherwise the tool exists for a user whose roles intersect it. Admin does not pass implicitly. The same predicate as `roles` in `model.json` (`ModelJson.CheckRoles`), so a role means one thing in the UI and here.

- **The roles are already in the ticket.** `AppUserStore` adds the `Roles` claim (and `Admin` for that role), the ticket is built by the cookie's factory, `CurrentUser` lays it out into `Identity.Roles`. Nothing is loaded from the database, per request or at issue; a change applies at the next refresh, ≤ 10 minutes — the sentence already true for lockout.
- **A property, not an attribute.** An attribute is forgotten silently (absent = open to everyone, or a start-up check), and cannot vary per instance — a provider's tools take their roles from data. Abstract: forgetting it does not compile. An array, not a comma string.
- **Rejected: a `CanView()` method.** Arbitrary logic in every tool; a list is checked by one code and read without the body.
- **Rejected: rights from the database by `UserId` (the previous decision).** `Permissions`/`IsAdmin`/`ReadOnly` come only out of the menu procedure (`ShellController.SetUserStatePermission` over `MENU_PROC`/`UserMenu`): the whole menu on every call, `ShellController` cut apart, `SetUserState` writing the `State` cookie into a Bearer response, and `menu.json` applications having no permissions at all. Putting that `State` into the ticket at `/token` was weighed too: it saved one query per call that touches the database anyway.
- **Not covered, accepted:** row-level limits (the user's companies) stay in the tool's SQL; an application whose rights are permission objects rather than roles is closed to MCP by roles.
- **Not built: delegation.** OAuth `scope` already travels in code and refresh and is checked nowhere. `read`/`write` chosen on the consent screen would narrow, never widen — "Claude reads but does not post". Waits for the first writing tool; then the tool's read-only flag is also MCP's `readOnlyHint`.

## Descriptions: the only text the model reads

- **Self-sufficient.** No references to a router prompt or its sections; a domain glossary (which word means which kind) goes to the server's `instructions`, sent once per connection, not repeated per tool. Rendering instructions are not written — the client renders.
- **Ids come from `resolve_names` only.** A platform tool: candidates `{text, kind?}` batched, substring match, per kind up to 10 `{id, name}`, `totalCount`, `truncated`; one → use, several → ask, none → say so, truncated → narrow. Its enum of kinds is built per request from the kinds the user's roles reach — a kind missing from the enum is unavailable, and the description says so. Each kind is instance settings: where to search, its roles.
- **No url in a result.** Relative to claude.ai it is a dead link, and the model starts composing addresses. Only absolute (from `mcp:publicUrl`) and only once a deep link into the shell is verified to open the record.
- **A tool with an id parameter whose kind the user cannot resolve stays listed.** The filter goes unused, as a report's field whose browse does not open in the UI; the resolver's refusal is the honest answer.

## Open (tools)

- **Where `IMcpTool` lives.** In `A2v10.Mcp` while only its own tools implement it; moving it is a file move. Decided when the metadata provider needs it — `A2v10.Metadata` must not pull the SDK, so the interface holds BCL types only. `A2v10.Infrastructure` is a candidate: a published-contract change, named before the commit.
- **`Company`.** The UI sets it with a switcher (`SetCompanyId`); `/mcp` has none. Default from the user, or a tool parameter.
- **The result boundary.** Over N rows a refusal "narrow the filters / grouping", never a silent cut. Where it is enforced — the tool or the handler — not decided.

## Idea: the whole system in 5k tokens (not decided, 2026-10-01)

**Budget.** Anthropic's guidance: tool search pays past ~10K tokens of schemas; 30+ always-loaded tools is a dated pattern, and near-duplicate tools hurt selection. At ~450 tokens a tool, ~20 per user is the ceiling without deferred loading — and whether a client defers is the client's choice (Claude Code does; claude.ai unknown). The count is per user, not per application: roles split it.

**In context — a map and the ways to walk it, not the system.** `instructions` ~800 (what the system is, one line per area, how to walk: map → info → call; ids only from find) + seven fixed tools ~2500 + 2–3 hot domain tools ~1500:
- `map` — areas → entities, reports, domain tools, as this user's roles see them;
- `entity_info` — what an entity is: properties, required, what the platform computes, which fields it is found by;
- `entity_find` — ids by name or code (was `resolve_names`);
- `entity_query` — a list with filters;
- `entity_crud` — create, read, update, delete;
- `tool_info` / `tool_run` — the description and the call of a catalogued tool.

**A tool has a delivery mode, not a second kind.** *Listed*: description and schema in `tools/list`, called directly. *Catalogued*: in `map`, described by `tool_info`, called by `tool_run`. `IMcpTool` is unchanged — only the road to the model differs; a report, a domain tool, a metadata report are all catalogued tools. Price: a catalogued question is 2–3 calls instead of one, and each description lands in the history; without a good one-line area in `instructions` the model does not know to look. It is `describe` → `call` again, but only for the catalogue — frequent tools stay typed in the list; the author decides which goes where.

**Entity tools are beside the domain tools, not instead.** Those answer questions (`cash_flow`), these enter and edit data. What an entity is reaches the model on demand; the cost stays fixed however many entities there are.

### The probe flow: "recognize this PDF and create an invoice for the customer"

0. **The PDF is read by the client.** The server takes no part: the model extracts the requisites — customer (with ЄДРПОУ), number, date, rows (item, qty, price), total.
1. **`map`** — skipped when `instructions` already name the area ("Sales: invoice, waybill…"); else one call to learn the entity is `invoice`.
2. **`entity_info("invoice")`** — `Date`, `No`, `Agent` (ref), `Company` (ref), `Rows[]` {`Item` (ref), `Qty`, `Price`}; what is required; that `Sum` is the platform's.
3. **`entity_find`, one batched call** — the customer by name or ЄДРПОУ, our company, every item of the rows. One each → on; several or none → the model asks the user ("is 'Папір А4 80г' 'Папір офісний А4' or a new item?").
4. **`entity_crud create invoice {…}`** — the client asks the user's approval (a write). The platform checks against the shape: violations come back as a list ("row 3: no `Item`"), the model fixes and resends; success → id and number. A draft, not posted.

4–5 calls, no new tool. The flow is simple; the difficulty lives in the quality of two answers — the search and the check on write:
- **Items by name miss.** The PDF carries the supplier's name, the catalogue its own; a substring over `Name` misses constantly. `entity_find` searches by code, article, ЄДРПОУ too, and `entity_info` says which fields an entity is found by. This decides how often the model asks back.
- **Reconciliation with the document's total.** The model sends qty and price, the platform computes the sum. A total that differs from the PDF (VAT inside or on top, rounding) comes back as a warning — otherwise "created successfully" with another sum.
- **A repeated create.** The answer is lost, the model resends — two invoices. Needs an idempotency key, or a check by (customer, number, date) before creating.
- **A cascade of new catalogue records.** A missing item can be created by the same `entity_crud`, but it has its own required fields (unit, VAT rate). "New catalogue records only with the user's consent" stands in `instructions`.
- **`Company`** resolves itself here — our ЄДРПОУ is in the PDF and `entity_find` finds it. A special case; the general question stays open.

Open, at the level of the concept:
- **What an entity is.** A catalog surely; a document with rows and posting — the same thing or another (posting is its own verb, not an update)?
- **Who may what.** With one tool for every entity, "may" belongs to the entity and the action, not the tool — the roles-on-the-tool rule does not reach it.
- **Delete is usually void** in A2v10; the verb must be named honestly, or the model promises a deletion that did not happen.

## Refresh bound to client_id: hygiene, not a barrier

`/token` compares `ClientKey` with the form's `client_id`, as RFC requires. The client is public: whoever has the refresh token has its `client_id` too.

## Implementation status (2026-09-30)

Pieces outside this project:
- `Identity/A2v10.Identity.Core`: `IOAuthConsent.cs`; `AppUserStore.RotateTokenAsync`.
- `Identity/A2v10.Web.Identity.UI`: `AccountController.Consent` GET/POST, `Views/Account/Consent.cshtml`, `ConsentViewModel`; the `TwoFactor` returnUrl fix.
- `Platform/SqlScripts/a2v10_security_simple.sql`: `a2security.RotateToken` (module version 8668).
- Localization `@OAuthConsent*`, `@OAuthAllow`, `@OAuthDeny` in `Platform/A2v10.Web.Assets/wwwroot/localization` and the copy in `Web/A2v10.Core.Web.Site`.
- `Web/A2v10.Core.Web.Site`: project reference, `services.UseMcp(Configuration)`, `Mcp:PublicUrl`.

Done and run on `Web/A2v10.Core.Web.Site`:
- routes: metadata 200, `POST /mcp` without token → 401 with `resource_metadata`, `GET` / `DELETE /mcp` → 405;
- `register`: loopback 201, `http://` non-loopback → `invalid_redirect_uri`;
- `authorize`: foreign redirect → 400 without redirect, no PKCE → redirect with `invalid_request`, not signed in → login and back, consent → redirect with `code` and `state`;
- consent screen: DCR name shown literally, loopback warning;
- `token`: code → 200 `no-store`; code replay → `invalid_grant`; refresh → new pair; old refresh replay → `invalid_grant` and the new refresh dead too (family gone); tampered Bearer → 401;
- `tools/call whoami` with the ticket → the signed-in user;
- through ngrok with a claude.ai custom connector: discovery → DCR → consent → token → `whoami` returned the user;
- Claude's own refresh: `whoami` 55 minutes after the token was issued (10-minute access) answered without a new sign-in. Proactive or after a 401 is not visible (ngrok does not log paths).

Not verified: refresh with a different `client_id`; Claude Code as a local client (Node must trust the dev certificate via `NODE_EXTRA_CA_CERTS`).

Not built: `IMcpTool`, the registry and its handlers, rights by roles, `resolve_names`, the metadata provider; `RotateToken` for multi-tenant scripts; "disconnect" in the UI.

# A2v10.Identity.UI

The account pages (`account/*`): each a document of its own with its own app, no shell.

## Pages on a framework: a framework-free box, a view per framework (2026-10)

Three axes, three places; a new framework adds a layout and a view per page, never a copy of a page's logic.

- **What the page knows and does: `Views/Account/<Page>Page.cshtml`**, framework-free JS, one expression `(function () { … })()`; a view takes it as `const page = <partial>;`.
  - The server's data as one JSON: `init`, `texts`, refusal codes → texts. Serialized with `JavaScriptEncoder.Create(BasicLatin, Cyrillic)`: Cyrillic readable in the debugger, `<>&'` still escaped, so the JSON cannot close the `<script>`. Never Razor inside JS string literals.
  - Rules in the model core's format (`std:validators`): a string is `notBlank` with that message, `{ valid: 'email', msg }`, a function `(state, value)` sees the other members.
  - What the server checks comes from the server, never a client constant: the password's length is `IOptions<IdentityOptions>.Value.Password.RequiredLength` in `init.passwordLength` (`ConfigureDefaultIdentityOptions` is only the default; a host's own options replace it whole). A fact two pages share is one C# value in `init`: the phone's mask is `PhoneMask.Value`.
  - `submit(state) → { url } | { error }`. Several steps on one page (ForgotPassword): `steps`, each `{ rules, submit }`, a step answering `{ next }`.
  - Reaches no runtime and no `window.location`. Posts by `__identityPost` (`Views/Shared/_IdentityPost.cshtml`, framework-free too): `{ url }` on a 302 or a JSON `Redirect`, `{}` done, `{ code }` a refusal, `{ error }` a status not 2xx; a page turns codes into its texts.
- **The runtime: the layout `_IdentityLayout.<fw>.cshtml`**, chosen by `_ViewStart` through `Framework.ViewName`. It loads the runtime, includes `_IdentityPost`, defines `__identityApp(options)` (the app on `#app`, `v-focus`).
- **Markup and glue: `<Page>.<fw>.cshtml`**, chosen by the controller: `View(_appOptions.Framework.ViewName("<Page>"), m)`. Binds its markup to the box; the validators, the mask (`std:mask`) and the redirect come from its own runtime.

Why the box and not one Vue options script for Vue 2 and Vue 3: a runtime that is not Vue (React) or a vocabulary that is not this markup (Fiori) gets nothing from a Vue options object. What is shared is what knows no framework; the model core (`@a2v10/model`, also delivered alone as `a2v10-model.js`) already is that.

- **Vue 3 takes `a2v10-core`**: Vue as `a2v10.platform.Vue`, `std:*` by `a2v10.platform.require`. Same URL as the shell (`?v=AppBuild`, `Environment.Minify`), so after the login it comes from the cache. `locale-<lang>.min.js` goes first: `std:validators` reads `window.$$locale` on its first require. The page's app is not attached to the platform.
- **Vue 2 is frozen**: `<Page>.cshtml`, `<Page>Script.cshtml`, `_UtilsScript`, `_IdentityLayout` stay as they were. Moving it onto the box needs `a2v10-model.js` delivered and a Vue 2 `Runtime` adapter, paid for a runtime that is leaving. Price: its pages' logic is a copy that dies with it.
- **A page with no logic is one view for every framework**: no Scripts section, no app, no Vue; the controller keeps `View(m)`, no `ViewName`. Every layout writes `v-cloak` on `#app` only when the page defines Scripts (`IsSectionDefined`), so such a page shows as rendered. Error, LoggedOut, Consent, License; Vue 2's views of them changed too: a dependency removed, not an adapter paid.
- **A page not ported fails loud on Vue 3**: the layout switches for every page, and `new Vue` throws by name (the core's `window.Vue` facade). No fallback to Vue 2.
- Ported: Login, Register, ConfirmCode, ForgotPassword, InitPassword, Invite, TwoFactor; Error, LoggedOut, Consent, License (no framework). All pages.

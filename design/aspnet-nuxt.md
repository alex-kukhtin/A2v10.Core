# An application on the metadata layer: ASP.NET + Nuxt

> Decisions, why, and what was refused. The canonical variant of an application that takes the metadata layer as its data and draws its own screens: an ASP.NET host, a Nuxt client, our model. Being invented, not reconciled - `D:\CLAUDE\VueCore` (a Nuxt + A2v10.Data skeleton) and `D:\CLAUDE\NewVue` (server-driven Vue 3 screens, events on data) are raw material, not specs. Its skill is written by the blind instance against the platform's anchors (/CLAUDE.md, "Skills as spec"). Decided 2026-10-15.
>
> The stand: `C:\A2v10_Net6\MetaAspNetStand`, beside the repository, eats Core from source (`$(A2CoreDir)` = `..\..\A2v10.Core\`), database `MetaAspNetStand` on localhost. Run on it 2026-10-15: one catalog (`catalog/agent`) - the index as a table, a new card, insert, update, a stale `rv` refused - through the browser. Built on the cut of [metadata-xaml.md](metadata-xaml.md).

## Server

- **The host takes the data layer alone:** `UseAppMetadata()` (no `UseAppMetadataXaml`), `UseApiDataServices(configuration)`, `UseSimpleDbContext()`. `NullReportEngineProvider` because `UseAppMetadata` registers the print handler. The user is `DefaultAdminUser` (99) until a login is asked for - a stub, not a decision.
- **The connection string is named `Default`**, set explicitly (`DataConfigurationOptions.ConnectionStringName`). The data layer's own default `DefaultConnection` was tried and refused: `a2` (`Tools/A2v10.Cli/Program.cs`) and `UsePlatform` read `Default`, so `a2 meta deploy` would find no connection.
- **No write-metadata cache flag**: a metadata save builds its TVP from `TableMetadata` (`DataTableBuilder`), never through a `.Metadata` procedure that flag caches.
- **Newtonsoft both ways** (`AddNewtonsoftJson`), with the platform's data protocol (`JsonHelpers.DataSerializerSettings`) plus two things it lacks: `DefaultContractResolver` (MVC's camelCase would rename the markers' keys, `Agents` → `agents`) and `ExpandoObjectConverter` (nested objects of a body as `ExpandoObject`, not `JObject`). The settings are copied member by member: MVC's `SerializerSettings` cannot be replaced. Refused: System.Text.Json - its `ExpandoObject` values are `JsonElement`, and a TVP is not built of them.
- **Its own database.** Sharing MetaAppStand's was refused: a host's first request deploys its metadata, so each would rewrite the other's `a2meta` seed and empty its `a2security.Grants` (the grants merge deletes) - until that host restarts.

## Controllers: per entity, written by the developer

- **A controller per entity, through `EndpointDataService.At("catalog/agent")`** - index, `new` (`CreateAsync`), load, save. The developer's own action sits beside the standard ones (`[HttpPost("domycommand")]`), with its own SQL if needed.
- **Mechanisms simplify boilerplate; they do not replace everything.** A base `PlatformController("/catalog/agent")` with attributes is the likely shape - built when the stand's friction asks, not before. Open: how an own action declares the right it needs on the address - the one place safety breaks silently (a bare `IDbContext` bypasses the endpoint's grants).
- **Refused: one generic controller.** The platform's `_data/*` (`DataController`) already is the generic protocol; a second over `EndpointDataService` would grow back into it. Refused: closing the host with ready platform code - the developer wants ordinary ASP.NET with simple places to write.

## The client's model: shape at build time, data on the wire

- **No form on the wire.** For a metadata endpoint the generator writes the SQL markers, so everything the runtime metadata says of the shape (types, keys, collections, main object, lazy fields) is known at build. What stays runtime is data already: `$ModelInfo`, row rights, blob tokens.
- **Classic endpoints (`model.json` + hand procedures) are not in this canon** - the application's folders hold `metadata.json` and nothing that describes a screen. Hence generation covers every model.
- **One generated artifact feeds both types and runtime**: `*.model.ts` beside the screen - TS interfaces for `vue-tsc`, and the type definitions the ported data model builds objects from (what `VueDataScripter` emits at run time today as `cmn.defineObject(...)`). Written by hand on the stand for now (`index.model.ts`, `edit.model.ts`).
- **The data part of `main.js` is ported once, as a package** - the model without components.
- **Rules declared must execute.** `rules` of `metadata.json` (required, visible, properties, inherit) compile into `template.ts` for the platform client and do nothing in this one, while `a2 meta validate` passes: a declaration with no effect. Retargeting the generator to the new model comes before real use.
- **A drift guard is cheap**: a test comparing the generated client shape with `BuildDataModelMeta` of the same endpoint - both sides exist.

## One entity, one folder

```
WebApp/catalog/agent/
    metadata.json        declaration
    AgentController.cs   HTTP
    index.vue  [id].vue  screens
    index.model.ts  edit.model.ts   their models
```

- **The host project is the container.** The module path is the host itself (`Application:Modules:Main:Path` = `.`); no separate `MainApp`. `metadata.json`, `app.json`, `@schemas` are not copied to the output (`Content Update ... Never`): a copy under `bin\` would be found by the walk and read as a second endpoint. Not verified: release - an `AppContainer` read from the host's own assembly.
- **Nuxt at the stand's root, its shell in `client/`**, pages from the host's tree: `dir.pages: '../WebApp'`; `ignore` for `bin/`, `obj/`, `*.model.ts` (else a model file becomes a route); `typescript.tsConfig.include` on `WebApp` (the generated tsconfig covers srcDir only - `vue-tsc` never saw a page); Tailwind `@source "../../../WebApp"`. `node_modules` at the root, the common ancestor, so a page outside srcDir resolves `vue`. Any `.vue` in an entity folder becomes a route: components live in `components/`.
- **Probed 2026-10-15:** `dotnet build`, `a2 meta validate catalog/agent` (all stages), the routes `/catalog/agent` and `/catalog/agent/:id`, `vue-tsc` catching a planted typo in a page and `null` against `string | undefined`.
- **To think (not decided): the layout knowledge in one place** - a Nuxt module shipped with the platform (`modules: ['@a2v10/nuxt']`) carrying the pages dir, the tsconfig include, the Tailwind source, and a `pages:extend` filter instead of ignore globs. Then a Nuxt upgrade breaks one module, not every application.

## Screens

- **Components, and screens composed of them by hand** - flexibility over screens generated whole from the declaration. The boilerplate is held by the size of the components (an entity table, a reference field with its selector, a collection of rows).
- **Nuxt UI** (Tailwind v4, Reka UI) - not bare Tailwind: table, form, inputs come with it. **`v-model.nullable`** on inputs: the wire carries `null`, Nuxt UI types `string | undefined`, and the modifier also writes an emptied field as `null` - the platform's rule (an empty string is stored as null).
- **Bundled pages** (`client` pages are built into the bundle): NewVue's "the bundle does not grow with screens" is not held. Accepted for a typical application.

## Dev and production

- **Dev is two processes**: `nuxt dev` on `:3000` (Vite compiles `.vue` live) proxies `/api` to the host on `:5320`; the browser sees one origin. **Production is one**: `nuxt generate` → static files in the host's `wwwroot`, no Node. Not built yet.
- Kept for now; the alternative is the host at the door in dev (`SpaProxy` / `UseProxyToSpaDevelopmentServer` - F5 starts both). `VueCliMiddleware` is the Vue CLI era (webpack); Nuxt stands on Vite.
- **Nuxt brings `vue` and `vue-router` itself**: explicit versions in `package.json` conflicted with Nuxt 4.6 (`vue-router/volar/sfc-route-blocks`).

## Next and open

- **Next probe: the whole flow on a document** - a header reference with its selector, a collection of rows, save, post. Everything so far is the easy case; the rest is a block of details.
- **Not built:** errors (a refusal reaches the client as 500 with a stack and a raw `UI:@[...]` - wanted `UI:` → 409 with text, `AccessDenied` → 403), localization of `@[...]` keys in data and errors, login, a wrapper over requests (pages call `$fetch` directly), the shell (menu, header), `full.sql` through `sql.json`, serving the static client, git on the stand.
- **The mart** is not this canon's - designed in its concept (`a2v10-md-skill/concepts/mart`). Carried there: a derived measure (`max(Closing, 0)`) is not additive and needs a declared grain; the trial balance already lays out per account before summing.

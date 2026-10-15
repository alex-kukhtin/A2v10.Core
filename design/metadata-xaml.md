# Metadata without XAML: the data layer and its pages, two packages

> Decisions, why, and what was refused. Decided and built 2026-10-15.
>
> Built: `A2v10.Metadata` holds no reference to the XAML view engine; the pages are `A2v10.Metadata.Xaml` (`Platform/A2v10.Metadata.Xaml`), registered by `UseAppMetadataXaml()`. Registered beside `UseAppMetadata()` in `Web/A2v10.Core.Web.Site`, `Tools/A2v10.Cli`, `Tests/TestsMetadata`; `A2v10.ApiHost` takes the data layer alone.
>
> Pinned by `Tests/TestsMetadata` (498 green at the cut): `ActionDispatchTests` (every action named by the template and the XAML), `XamlBeamTests` (every screen written, read back, written again), `TsBeamTests`, `PaletteTests`, the validator's tests. Not verified live: a rendered page after the cut — cached forms, the print page, the tag/attachment/operation dialogs, the users screen, reports. Tests do not pass through render. MetaAppStand (outside the repo) registers both in `WebApp` since 2026-10-15; builds.

## Why

- **One layer, two kinds of host.** The platform host draws pages with XAML; a host that serves data only — an API, the backend of an SPA (a Nuxt client with its own model) — needs declarations, deploy, SQL and validation, and no view engine. Before the cut, `UseAppMetadata` pulled the XAML engine into both.
- **Not a split of the layer into "SQL" and "the rest".** Weighed and refused: a package of `TableMetadata` + deploy, with the runtime SQL left behind. The runtime SQL reads the endpoint (`Storage` + `Declaration`: filters, `Fixed`, operations), never a form — so the unit that travels is the endpoint without its pages, and the whole layer goes; only the drawing is cut off.
- **Not a new protocol.** A data-only host reaches endpoints through `EndpointDataService.At(route)` (API, CLI, tests already did). A generic controller over it was refused: the platform's `_data/*` already is the generic protocol, and a second one would grow back to it. A host's controllers are its own, written per entity (see "Not built").

## The seam: `IPageRenderer`

- **One question a builder asks the view engine** (`Interfaces/IPageRenderer.cs`): `RenderPageAsync(PageRequest)`. A builder loads the model and chooses the template; the page is the renderer's.
- **`PageRequest` is one record** — endpoint, url, data source, view, model, template. A new fact of the page is a member, not a parameter. Which page is the endpoint's TYPE: no string tells the renderer what it is.
- **The template stays in the layer.** It is JS for the platform client, not XAML. A template written in `model.json` is read by `BaseModelBuilder.TemplateAsync`; `CodeLoader` keeps the XAML view only.
- **No renderer is a refusal by name** (`IPageRenderer.Of`: "no page renderer is registered - this host serves data only"), worded once for every builder.
- **A reload never reaches the renderer.** Every builder returns the data alone on `isReload`. The tag, attachment and operation builders used to render regardless (DataService dropped the page) — changed: without it a data-only host failed on them.
- **The renderer is a second dispatch on the endpoint's type** (`XamlPageRenderer`), beside `ModelBuilderFactory`'s: that one knows what an endpoint serves, this one how its page is drawn. A type with no arm is refused by name.
- **`XamlBuilder` takes the endpoint and the url, nothing of the builder's** — no data source, base or grants reach a page. Its descriptor carried them and it read two.
- **Two more members, one per consumer:** `ViewText(endpoint, action)` — the generated view as text, for `a2 meta materialize`; `BuildPages(endpoint)` — every page drawn without data, built and dropped, for the `screen` stage of validate.

## What moved, what stayed

- **Moved to `A2v10.Metadata.Xaml`:** `Form/*Xaml` and `FormButtons`, `XamlBuilder`, `XamlTextBulder`, `XamlExtensions` (`ToXaml*`, `BindSheetCell`), `CodeLoader`, the tag/attachment/operation dialogs, `Admin/XamlUsersBuilder`, `Reports/*ReportPage`, `XamlPageRenderer`.
- **Stayed in the layer, because the layer reads them:** `DefaultFormBuilder` (forms are data, `FormMetadata`), `ColumnTypePredicates.IsNumber`/`IsCurrency` (the default form), `StringExtensions.Localize` and `ReportItemMetadata.LocalizeLabel` (the report's SQL sends the labels back). What a domain IS stays; how it DRAWS moved.
- **Namespace unchanged** (`A2v10.Metadata`): the move touched no call site.

## Cache

- **The form a page caches lives in the layer's slot** — `DatabaseMetadataCache.GetOrAddArtifact<T>`, keyed by type, data source, endpoint path, action. One invalidation (`ClearAll`); the view engine has no watcher of its own. The key gained the type so two consumers with one key cannot read each other's object; the granularity is the old one.
- **`InitComplete` is the builder's** — the form is initialized before it is shared, inside the factory the renderer hands the slot. A XAML requirement the cache need not know.
- **Not cached, as before:** the print page (depends on `?Form=`), report pages (depend on the picks in the query), the system dialogs, the template, a hand-written view (`IXamlPartProvider` has its own).
- **`SetPlatformUrl` on a cached page is a no-op**: only `AutoPages.IndexPage` implements it, and the generator never builds one. Whether `RenderElement` mutates a shared page is the engine's property, not checked here.

## Reports: two halves

- **The query and the template** — `BaseReportBuilder` and its types; **the page** — `ReportPage` and its types (`Turnover`, `Ledger` → `TrialBalance`, `Chessboard`).
- **The page resolves its own `ReportGrouping` from the same url** the query does, so the two cannot see different picks. No builder state crosses the seam.
- **Two switches on the report type** — `BaseReportBuilder.Create`, `ReportPage.Create`. Validate builds both for every report, so a type taught to one fails there. Refused: dispatching the page by the builder's C# type — the renderer would have to build a query builder (platform id, grants) to draw a page.
- **"A fixed report has no choices" is stated twice** — `LedgerReportBuilder` (the template) and `LedgerReportPage` (the taskpad). Both halves need it; accepted.

## Admin

- The users screen: data and the template in `UserAdminBuilder`, the dialog per action in the renderer (`UsersPageAsync`). Two switches on the action, as `BaseModelBuilder` has; the builder refuses an unknown action before the load, so the renderer never sees one. The card's tabs per `boundary` dimension are read by both through the provider.

## Validate and materialize

- **Both reach XAML through the renderer.** No renderer is a FAILED `screen` stage, never a skipped one — an unchecked view must not read as a passing one. The stage set and the CLI output are unchanged.
- **`EndpointMaterializer` takes `IServiceProvider`**: `IPageRenderer` is internal, a public constructor cannot name it. Tests resolve the materializer from DI.

## Colours: a list of the layer's own

- **`Meta/Palette.cs` — the colours a declaration may name**, lower case as stored. It was read from `TagLabelStyle`, the XAML control's enum: a declaration is read by clients that are not XAML.
- **Held to the stylesheet by `PaletteTests`** (`.color-<name>` in the package's css). No test ties it to `TagLabelStyle`: the `TagLabel` draws `.tag-label.<name>`, another selector — two lists, each with its own CSS, not one fact.

## Price

- **`InternalsVisibleTo("A2v10.Metadata.Xaml")`.** The pages read the layer whole — constants, baked forms, internal members of the shape. Publishing them would promise hundreds of members nobody meant to keep.
- **Hence one version**: `A2v10.Metadata`, `A2v10.Metadata.Xaml` and the `a2` CLI ship together (/CLAUDE.md, "Release"). A pair of two versions fails at run time on a member that moved, not at the build of the host.
- **A host that renders registers two calls**, `UseAppMetadata().UseAppMetadataXaml()`. Forgotten, every page is refused by name on the first open.

## The direction this cut serves

The application on the data layer alone — an ASP.NET host, controllers per entity, a Nuxt client with our model: [aspnet-nuxt.md](aspnet-nuxt.md). The next cut — the client on Vue 3 in shelves, a view engine shipping its own components, a replaceable shell, a second engine: [view-engines.md](view-engines.md).

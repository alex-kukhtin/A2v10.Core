# View engines and the client: packages on shelves, contracts cut between them

> Direction, decided 2026-10-15; nothing here is built. The steps: [roadmap.md](roadmap.md). What the ASP.NET + Nuxt experiment ([aspnet-nuxt.md](aspnet-nuxt.md)) settled: the cut of the renderer ([metadata-xaml.md](metadata-xaml.md)) is useful on its own, and it is the first of several. The client moves to Vue 3 split by what each part depends on; a view engine becomes a self-contained shelf - its vocabulary, its renderer and its client components in one package; the shell becomes replaceable.

## Axes of change: the test of the layout

The design is tested not by a list of pains but by the axes along which the system can change at all. A pain is a projection; the structure is the axes. **Beauty: every axis isolated in one shelf**, so a change along an axis costs one shelf - a sum - not "a shelf × the screens × the shells" - a product. Decided 2026-10-15.

| axis | what changes along it | shelf |
|---|---|---|
| client runtime | Vue 2 → Vue 3 → the next | the runtime adapter of the platform (client); primitives (below) - never the model core |
| vocabulary, UI style | XAML, Fiori, Odoo… | a view engine |
| hosting, navigation | tabs, a page, Nuxt, a mobile stack, a portal | the shell |
| density of layout | desktop, phone, print | an engine's renderer |
| source of a screen | hand-written XAML, forms of `metadata.json`, generation | the form grammar → the tree |
| shape of the domain | entities, fields, rules | `metadata.json` |
| variant for a customer | country, partner, client | extension layers |
| behaviour | posting, actions, verbs | boxes with declared ports |
| storage, access | SQL, procedures | the data layer |
| consumer of the data | a human, an LLM through MCP, integration, BI | one data core, several faces |

- **Today's architecture is products of axes**, and its pains are their shadows: the runtime fused with the vocabulary (`RenderElement` writes Vue 2) - the move to Vue 3 is runtime × vocabulary; the shell fused with the components (a bundle per shell) - eight copies of `main.js` are shell × delivery; the density of layout fused with the screens (XAML keeps the desktop layout) - a mobile version is density × the number of screens.
- **Products left in the new layout, found by this lens:**
  - **Runtime × engines — removed by a shelf of primitives.** Components belong to an engine; with three engines the next change of runtime would be paid three times. So under the engines lies **one set of primitives per runtime** - a field, a list, an overlay, a layout - and every engine's components are composed of them. A change of runtime is one shelf of primitives.
  - **Form kinds × engines — kept, as a named price.** Every new kind of node in the form grammar needs an arm in every renderer; the completeness test (contract 6) makes it loud, the product stays. The grammar grows rarely and dearly, the vocabularies cheaply.
- **Every new decision is checked against this table**: which axis it moves along, and whether it touches one shelf. Two shelves for one axis is a product coming back.

## The switch: Vue 2 / Vue 3

- **The switch is the choice of a view engine, made by registration** - as `IPageRenderer` is chosen today; no flag inside a renderer, a scripter or a shell. Three engines:
  - **`A2v10.ViewEngine.Xaml` — frozen.** The vocabulary and its Vue 2 render, bit for bit as today. Running systems risk nothing; Vue 2 is not migrated away, it dies off as applications move.
  - **`A2v10.ViewEngine.Xaml3` — the same vocabulary, drawn for Vue 3.** It reads the same XAML into the same C# classes and does not copy them: two copies of a vocabulary part on its first new property. It renders by an **external visitor** dispatching on the node's type - the Vue 2 render stays inside the frozen classes (`RenderElement`), untouched. An unknown type is refused by name, and a test holds that every class of the vocabulary has an arm. `XamlBuilder` builds the same tree, so the forms of `metadata.json` reach Xaml3 with no builder of their own - only its `IPageRenderer`.
  - **`A2v10.ViewEngine.Fiori` — a vocabulary of its own** (floorplans), alongside; its forms come through `A2v10.Metadata.Fiori`.
- **Acceptance: a production application, not a sample, moves from Vue 2 to Vue 3 with not one line of its `ts`, `xaml` or `sql` changed.** Whatever has to be touched in it is a defect of the platform, fixed there.
- **Three renderers make contract 6 real at once**: the form grammar defined once, as data, and one test holding each engine complete over it.

## The shelves

| shelf | holds | depends on |
|---|---|---|
| **model core** | the data model (`TRoot`, `defineObject`, arrays, `$ModelInfo`, validators, events on data) - `datamodel.js`, `impl/array.js`, `validators`, `utils`, `period`, `modelinfo`, `log` | **nothing** - no Vue, no browser; the runtime reached through `std:platform` and `std:eventBus` only |
| **platform (client)** | the runtime adapter (`std:platform`: `set`, `defer`, `File`, `performance`; `std:eventBus`), the `_data/*` protocol, commands (`$save`, `$invoke`, `$dialog`, `$requery`) | the model core; one runtime - no renderer, no components, no shell |
| **primitives** | per runtime: a field, a list, an overlay, a layout - what every engine's components are composed of | the platform (client); one runtime |
| **view engine** - `A2v10.ViewEngine.Xaml` today | the vocabulary (C# classes, read from XAML), the renderer (tree → template), **the client components** the template names, composed of the primitives | the platform (client) and the primitives; its own C# side on `A2v10.Infrastructure` |
| **metadata pages** - `A2v10.Metadata.Xaml` today | forms of `metadata.json` → the engine's tree (`XamlBuilder`), `IPageRenderer` | the metadata layer + one view engine |
| **shell** | hosting screens: tabs (MDI), the plain page, or Nuxt; navigation, dialogs, confirm, toast | the platform (client); not a renderer |

- **The model core does not depend on Vue — and never did.** Verified in the `A2v10_Net48` sources 2026-10-15: `datamodel.js` and everything it requires name `Vue` nowhere (its `$set` is the model's own, `obj.prototype.$set = setElement`); the runtime is reached only through `std:platform` (`set` ×7, `defer` ×3, `File`, `performance`) and `std:eventBus` (`validators.js`, `pendingValidate`). The seam has stood since 2015 with two implementations: `platform/webvue.js` (~30 lines: `Vue.set`, `Vue.nextTick`, `new Vue({})` as the bus) and `server/platform.js` (plain assignment, an immediate call, an empty bus). It was cut for a real second consumer - the server - which is why it held. The Vue 3 adapter is a third file of the same shape: `set` a plain assignment (the proxy tracks it), `defer` Vue 3's `nextTick`, the bus an emitter with the same API. What remains of Vue in the core is the bus's API shape (`$emit`/`$on`) and `defer` meaning "after the tick" - both behind the seam.
- **The model's rules run on the server too, on Jint.** The same model core, loaded into Jint (already in `A2v10.Services`) with a server adapter of `server/platform.js`'s shape - plain assignment for `set`, an immediate call for `defer`, an empty bus. Running one record's rules on a save is nothing for an interpreter. So a write through MCP (`entity_crud`, to come) or an API obeys the same rules as the screen: one declaration, enforced on both sides - the class "checked in the UI, not through the API" closed. Decided 2026-10-15, not built.
- **Enforced by packaging, not by a test.** Every shelf is built as its own package, and a package lists only the dependencies its shelf has: the model core has no `vue` at all, so an `import 'vue'` in it does not resolve and the build fails. The same move as `A2v10.Metadata` without a reference to the XAML engine: a wrong dependency is not caught - it cannot be written.
- **The components ship with the view engine, not with the platform.** The renderer's output IS the components' API: shipped in one package with one version, the two cannot drift. A second engine brings its own components and touches nothing of the first.
- **A second engine is a second pair of shelves**: `A2v10.ViewEngine.Xaml3` (its renderer, its components, the frozen engine's vocabulary) with its `IPageRenderer`; a Fiori-style vocabulary (floorplans - list report, object page, sections, field groups) as `A2v10.ViewEngine.Fiori` (vocabulary, renderer, components) and `A2v10.Metadata.Fiori` (forms → its tree, its `IPageRenderer`). A host picks one by registration.
- **The shell is replaceable - the last point of the split.** A host picks one shell as it picks one engine: tabs (MDI), the plain page, Nuxt, or its own. The platform reaches it through contract 4 alone; a shell knows neither the engine nor the metadata layer, so swapping it touches no screen.
- **A shelf is a boundary of sources and dependencies, not of delivery.** How shelves are glued for the browser is the bundler's configuration: the probe's "one bundle, one tag" (`Vue3_TestStand/DECISIONS.md` §1) and separate packages are two settings of one layout.
- **Today the shelves are mixed**: the client JS is `main.js`/`tabmain.js`, glued from `A2v10_Net48` sources and shipped by `A2v10.Web.Assets` - the data model, the components and two shells in one file per shell.

## The contracts to cut

Each is a seam with two sides; each needs a written form both sides are checked against - the failure on every one of them is silent (an inert tag, an `undefined`, a command nobody answers).

1. **Screen ↔ platform: the `template.ts` API — frozen.** `properties`, `validators`, `events`, `commands`, `delegates`, the `$`-members of elements. It is the applications' code: kept, every A2v10 application moves from Vue 2 to Vue 3 without touching a template. With the XAML sources of the applications, it is the whole compatibility surface.
   **Its semantics hold by construction — probed** (`D:\CLAUDE\Vue3_TestStand`, `DECISIONS.md` §3, `RESULTS.md`): `reactive()` over the model's existing `defineProperty` accessors. The write hook is the accessor's setter — Vue 3 calls it through `Reflect.set` — so the model's events (`changing` with the right to cancel, `change` with the old value, `$setDirty`, `_lockEvents_` telling a load from a change) stay synchronous and never pass through Vue's scheduler; `this` in a setter is the proxy. Three local changes in `datamodel.js` (`configurable: true` on hidden props, `markRaw` on the shadow and metadata, the shadow in the accessor's closure); every configuration measured faster than Vue 2.7. The rule it leaves: one path in, one path out — a raw reference is never handed out.
2. **Renderer ↔ components: the vocabulary schema.** The tags and props the renderer writes are the components' API. One machine-readable schema, derived from the C# vocabulary classes (they already hold every property and type), checks both: the renderer writes nothing it lacks, every component accepts what it lists; it also types the components for `vue-tsc`. Xaml3's visitor writes Vue 3 from the start; the Vue 2-isms of today's output (`v-model` through `value`/`input`, `.sync`, `.native`, filters, `slot-scope`) stay in the frozen `RenderElement`, and the applications' XAML is untouched by either.
3. **Components ↔ platform: the element and the collection.** What a component reads and calls on the model: an element's value, `$valid`/`$errors`, an array and its `$ModelInfo` (paging, sort, filters as a collection view), a command with its context. Components depend on the platform, never the reverse.
4. **Platform ↔ shell: the bus, written down.** Navigation, dialog, close, confirm, toast, requery - about thirty events today (counted by NewVue), implicit. Written as an interface, it makes the tabbed MDI, the plain page and Nuxt three implementations of one contract; the shell knows no renderer.
5. **Server ↔ client: the data protocol, versioned.** `_data/*`, the markers' shapes (`$ModelInfo`, `$RowCount`, maps, keys), the refusals (`UI:` → text, `AccessDenied`). Both delivery modes speak it: a server-rendered page whose script defines the types at run time (`VueDataScripter` → `cmn.defineObject`), and a build-time `*.model.ts` making the same calls - one `defineObject` API, so the two do not compete. **The type names are part of it** (`TRoot`, `TAgent`…): `datamodel.js` dispatches by `constructor.name`, so a generator writes exactly the server's names and a minifier keeps them (`keep_fnames`).
6. **Form grammar ↔ renderers: defined once, as data.** `is: grid`, `treeGrid`, `tabs`, `filters`, `toolbar`… are the metadata layer's; today their check is "a second reading of `XamlBuilder.ElementToControl`" (Metadata/CLAUDE.md, "Forms") - the grammar defined through one renderer. With two, every kind is the grammar's, and a test holds that each renderer has an arm for each kind; otherwise the second engine silently draws less than the first.
7. **Localization: one owner per key.** `@[…]` keys travel in page text (the renderer's), in data (labels, operations) and in refusals. Which side translates each of the three is decided once, or two dictionaries grow side by side.

## Extension layers: named nodes, added to, never patched

The goal SAP-scale systems live on - vendor → country → partner → customer, each a layer over the one below, no fork, surviving an upgrade. Decided 2026-10-15.

- **Refused: patching a foreign tree by path** (Odoo's `<xpath expr=… position=…>`). It couples a layer to the inner structure of another's form: moving a field to another group breaks every patch, and two layers patching one node depend on the load order - a fragile base class in markup.
- **Layers extend the shape, merged by name.** A layer declares its fields and rules over the delivered `metadata.json`, merged by key with "mine wins" and the layer's rank - the mechanism `rules` already have. A name does not depend on where a field sits on a screen.
- **Screens are derived, not patched.** Default forms are built from the shape, so a field a layer adds appears with no line of screen.
- **A node the author names is an extension point; an unnamed one is closed.** Groups, tabs, fields - explicitly named by the base form's author, who thereby consents to extension there. A layer addresses a node by name only, never by path or position; names are unique within a form.
- **Layers only add**: a field into a group (at the end, or before/after a field named by name), a group into a tab, a tab into a form. No delete, no replace - hiding a foreign field is a `visible` rule, not an edit of structure. Two layers adding to one group do not conflict: the rank orders them.
- **An unknown target is refused at load**: `a2 meta validate` names both files. Renaming or removing a named node is a visible change of a contract, not a silent break at a customer.
- **"Forms: whole or nothing" stands** (Metadata/CLAUDE.md): a declared form still replaces the default whole; what is new is that its named nodes can be added to.

## The table: ideas from `A2v10.SpreadSheet`

The grid is drawn anew, by the ideas of `C:\A2v10_Net6\A2v10.SpreadSheet` (a spreadsheet - another task; its ideas only). They meet the bottleneck `Vue3_TestStand` measured: the cost is rebuilding cell components (500 rows - 113-157 ms in every configuration), not reactivity.

- **Scroll by index, not by pixels**: the position is a row/column number, an own scrollbar in items (`min/max/pos/page`), only the rows that fit are drawn - the DOM is the visible cells whatever the data. Virtualization by construction, no library. A fixed row height makes `rowFromPoint` a division.
- **A cell is not a component**: one render function writes cells as plain vnodes at absolute positions; a column's type decides format and alignment once per column.
- **Layers**: grid and headers, cells, selection, editor. The selection is a rectangle over the grid, not a class on cells - changing it redraws no cell.
- **Geometry is the one source** (`colWidth`, `rowHeight`, `rowFromPoint`, `colFromPoint`, `selRect`); one set of pointer handlers on the container with pointer capture, none per cell.
- **Frozen rows and columns** come first in the drawing order - no CSS `sticky`.
- **One editor over a cell** (F2, double click), committed on blur - here the typed field of the column (a reference with its selector, a date, a sum); it writes through the model's setter, so the model's events fire.
- **Keyboard on the container** with the scroll following the active cell; a column's width resized by an overlay line and kept in the column's declaration; spans through a merge map for multi-level headers and group rows; `ResizeObserver` for the page size.
- **Over data**: rows are model elements (proxies), `row[col.key]` is the value, the selected row is `$selected`; scrolling works inside the loaded window, `$ModelInfo` pages or loads the next window at the edge.
- **Not taken**: A1 addressing, styles per cell, `contentEditable` as the editor, the sheet's editing toolbar, `Vue.set`.

## Ideas from Odoo

Its look is not the point; what closes classes of tasks:

- **Many views of one entity** - form, list, kanban, search, pivot, graph, calendar, with a switcher. **Kanban by `state`** is nearly free over our States (columns are states; dragging a card is a transition - the verb waiting for its declaration); search is our filters namespace; pivot and graph are the mart.
- **A status bar in the header** - the natural UI of `state` and its `Role`.
- **Chatter** (messages, attachments, history on any record, by a mixin) - our traits: `attachments` exists, comments are [texteditor.md](texteditor.md), history is the planned `audit`; one trait `chatter` gathering three.
- **A widget per field type, overridable** - our field by `ColumnType`.
- An Odoo-style engine would be a third vocabulary - author-written views over the same form grammar (contract 6), where Fiori is floorplans derived from the declaration.

## Open

- **One component library, two distributions.** A server-rendered page loads the components as static assets of the NuGet package (`_content/…`, as `A2v10.Web.Assets` copies scripts today); an SPA built by Nuxt imports them from npm. One source, two packagings, one version - how is not decided.
- **The versions line up across ecosystems.** `A2v10.Metadata`, the CLI and `A2v10.Metadata.Xaml` ship as one version already (/CLAUDE.md, "Release"); the client platform and the components join it, on npm. Contracts 2 and 5 live on both sides of that line.
- **Where the client sources live**: an npm workspace (platform, the XAML engine's components, shells) - in this repository or beside it; `A2v10_Net48` stops being the source.
- **The look changes**: components on a new kit (Nuxt UI or another) draw differently from today's CSS - a decision about appearance, taken per engine.

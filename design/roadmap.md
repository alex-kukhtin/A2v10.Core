# Roadmap: from the cut of the renderer to engines, layers and the SPA canon

> The step-by-step plan, agreed 2026-10-15. The order is set by dependencies; every step ends with a beam - what proves "done". Why each step: [view-engines.md](view-engines.md) (shelves, contracts, the switch, extension layers, the table, Odoo), [aspnet-nuxt.md](aspnet-nuxt.md) (the SPA canon), [metadata-xaml.md](metadata-xaml.md) (the cut done). The mart is not here - it lives in its concept.

## Step 0. Close what was done

- **0.1** Review and commit the cut of Core (the author). *Beam:* 498 tests green, the stands open.
- **0.2** Escape the query string in `EndpointDataService.CreateIndexQuery` - `$"{x}={d[x]}"` and `SortOrder` written raw, so a search fragment with `&`, `#`, `+` becomes another query. *Beam:* a test with those characters in the fragment.
- **0.3** ~~Record the decision on extension layers~~ - done: [view-engines.md](view-engines.md), "Extension layers".

## Stage 1. Contracts as code, no implementation - everything else stands on them

- **1.1** The form grammar as data (contract 6): the table of node kinds, today "a second reading of `XamlBuilder.ElementToControl`", moved into the metadata layer and read by `FormElement.CheckElement`. *Beam:* a test that `Metadata.Xaml` has an arm for every kind. The first step that moves the whole design: all three engines and the layers need it.
- **1.2** Names on groups and tabs in the grammar, unique within a form. No layers yet.
- **1.3** The vocabulary schema out of the C# classes of `A2v10.Xaml` (contract 2), a generated artifact. *Beam:* a test that the schema covers every class.
- **1.4** The shell interface (contract 4): the bus events of `tabbed.js` as a typed `.d.ts`.
- **1.5** The data protocol (contract 5): `_data/*`, the markers, the rule of type names - as a `.d.ts`.
- **1.6** The `template.ts` API (contract 1): today's `platform.d.ts` fixed as the frozen contract.

## Stage 2. The client platform on Vue 3 - the beam first, then the port

- **2.1** Where the npm workspace lives - decide.
- **2.2** The bundle switch on the back end. First, because without it there is no pairwise run (`Vue3_TestStand/DECISIONS.md` §6).
- **2.3** Characterization tests on the old bundle: the `services` specs to vitest, DOM-level component tests, a Playwright frame for two bundles.
- **2.4** Port `datamodel.js` with the three changes, and the identity probe on the real model. *Beam:* one test set green on both runtimes.
- **2.5** Port the services: http, `$ModelInfo`, commands, the bus as an emitter.

## Stage 3. The Xaml3 engine - the main stage

- **3.1** Freeze `A2v10.ViewEngine.Xaml`.
- **3.2** The `A2v10.ViewEngine.Xaml3` frame: a visitor over `UIElement`, an unknown type refused by name. *Beam:* the completeness test against the schema of 1.3.
- **3.3** The new table, by the ideas of `A2v10.SpreadSheet` - scroll by index, layers, an editor over the cell, frozen columns. A step of its own: the heaviest row of the estimate, and the base Fiori builds on.
- **3.4** The components on Vue 3, in the engine's package, composed of a shelf of primitives (a field, a list, an overlay, a layout - [view-engines.md](view-engines.md), "Axes of change"): the primitives first, then simple controls → containers → selector and collection view → the plumbing of `base.js` and dialogs.
- **3.5** `Metadata.Xaml3`: its `IPageRenderer` over the tree `XamlBuilder` builds.
- **3.6** The tabbed shell on Vue 3, by contract 4.
- **3.7** **Acceptance**: a production application on Xaml3 with not one line of its `ts`, `xaml` or `sql` changed. *Beam:* the pairwise Playwright run.

## Stage 4. Extension layers - grammar-level, in parallel with stages 2-3

- **4.1** Layers of `metadata.json`: fields and rules merged by name, "mine wins" with the layer's rank.
- **4.2** A layer's operations over named nodes: add a field, a group, a tab; before/after a field by name. *Beam:* `validate` refuses an unknown target naming both files.
- **4.3** Default forms place the layers' fields by themselves; a slot node for hand-written forms.

## Stage 5. The Fiori engine

- **5.1** The vocabulary (~25 elements), the visitor, the components over the table of 3.3 and the fields.
- **5.2** `Metadata.Fiori`. *Beam:* completeness against the form grammar of 1.1 - the same test, the second renderer.

## Stage 6. The SPA canon (ASP.NET + Nuxt)

- **6.1** The generator retargeted: `*.model.ts` with TS types and `defineObject` calls; the rules executed.
- **6.2** Refusals (`UI:` → 409, `AccessDenied` → 403) and the localization of `@[…]` keys.
- **6.3** Nuxt as a shell by contract 4; think through the `@a2v10/nuxt` module.
- **6.4** The whole flow on a document: a selector, a collection of rows, posting.

## Stage 7. Ideas from Odoo

- **7.1** Kanban by `state`, and the transition verb.
- **7.2** The status bar in the header.
- **7.3** The `chatter` trait: attachments, comments, history.

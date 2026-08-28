# A2v10.Core

A .NET platform for enterprise web applications (author — Oleksandr Kukhtin). A set of NuGet packages converging on `A2v10.Infrastructure`.

The real core is an end-to-end **data-binding engine** (IDataModel: SQL ↔ C# ↔ reactive Vue), not the metadata layer. A `_data/*` request → `DataService` → `IDbContext.LoadModelAsync` builds a hierarchical `IDataModel` from a procedure's recordsets by naming conventions; `VueDataScripter` bakes it into a self-contained Vue component; Save posts the model back via a TVP.

`Platform/A2v10.Metadata` is an **active** declarative ERP/scaffolding layer on top of the core (not a stub): from an entity description (`TableMetadata`: Catalog/Document/Operation/Journal/Details) it either renders pages at runtime (SQL is generated on the fly and feeds the same IDataModel pipe) or generates sources (model.json/vxaml/ts) and DDL.

---

> This file is a **durable artifact of decisions**, not documentation on top of the code.
> Intent lives here; `.cs` is its projection (a compile target). Decisions go here — visibly and under git. Don't clutter memory (`~/.claude`) with them.

## Primary goal: an LLM-aware platform

Make the platform LLM-aware with **small, relative add-ons**. The aim: a simple decision fork and fast/cheap creation of real applications.

This is **not** "add AI." It's designing so that **trust is structural** — letting an LLM into the platform is safe *by construction*. Trust is a property of the loop, not of the model: if drift is visible and cheap to catch, it's safe to let it into real code.

### Three things that make trust structural (in priority order)

1. **A tight, authoritative feedback loop.** Validate + instantiate + result-JSON in ~1-2ms. The load-bearing beam. Build it FIRST, before any DSL/facade. Without a ground-truth beam an LLM-aware DSL is just a DSL the model drifts across beautifully. **Loop first, facade second.**
2. **High decision density at the surface.** The platform absorbs boilerplate decisions into itself; only the domain "what" remains on top.
3. **Frozen judgment as part of the platform.** A good cut baked into the framework; an LLM working inside inherits the right decisions for free.

### The design axis: bounded vs ambient (NOT declarative vs imperative)

Imperative code isn't the danger — **ambient authority** is (when code reaches for what isn't in its signature: DB, file, globals, the clock, the network). Imperative code is fine in any amount **inside a box** with a tight typed in/out and zero ambient authority. The body can go unread — you verify at the seams.

The author's imperative code is already hidden behind simple in/out interfaces, "unbreakable by design" → the most expensive part is already done and turns out to be ideal for an LLM. Rule when extending: deliver a new capability not through an ambient hatch but through a box with declared ports.

### Where the risk remains

Boxes are safe by construction — only **composition** can lie: a wrong graph assembled from correct boxes. Risk concentrates in the **composition/intent** layer — where the human sits.
Consequence for the ground-truth beam (#1): verify the **assembled result at the assembly/application seam**, not individual boxes.

## Working method (how to run work in this repo)

This repo has **little code and many decisions** (rare; typical enterprise is the reverse). The repo's cleanliness (no legacy) is a consequence of the author's cuts, not of generation. Therefore:

- **Code is a compile target.** Output wrong → fix the intent (this file / a test), regenerate. Don't patch `.cs` to silence a symptom. Correctness is checked by behavior (test green, tool returned the expected JSON), not by reading generated code.
- **Make decisions, don't hand them back.** Where the cut is obvious — cut, and say what was cut. No "menu of two tracks." Default to **"don't build"** over "build the general case"; typical, not exhaustive; defer dead code.
- **Don't touch working code you weren't pointed at.** Don't restructure the unasked.
- **Probe, don't theorize.** Where a fast ground-truth beam exists (build / test / validate+JSON) — run it first, then assert.
- Don't "extend to the uncomfortable end" in every reply for the sake of beauty — that's agreement-as-performance instead of a decision. Restraint here is the judgment being asked for.

Division of labor: the human designs the boxes and holds composition/intent (the decision-dense work); the LLM fills boxes with imperative code; the platform guards that nothing escapes the frame.

## The `a2` CLI: shape of the command tree

`Tools/A2v10.Cli` (package `A2v10.CLI`, command `a2`) is the model's feedback loop — the ground-truth beam of #1. Its tree is a surface navigated blind, so its shape is a decision, not styling. Decided 2026-08.

- **A first-level command is never a verb.** The top level holds areas only (`app`, `db`, `endpoint`, `meta`, `view`); verbs are leaves inside them. Break it once and every later capability has two possible homes (`validate view` vs `view validate`) — placement stops being derivable and the model guesses. A cross-cutting action is not a fifth area: it is a leaf repeated in each area it applies to.
- **A group is named by the platform role, not by the file format.** `view validate`, not `xaml validate`. `view` is the platform's own word (the `view:` key of `model.json`); `.vxaml`/`.xaml` is a per-project convention, and one xaml format carries two unrelated element sets (views, report templates) — a format-named group promises both and delivers one.
- **Picking the area costs the model nothing; the output shape costs it everything.** Measured: it never picks the wrong area for a file it just edited. So don't buy facades that save it a token (dispatch by extension, one universal `validate`) — that budget belongs to the result JSON: one shape per command, findings distinguishable from "the tool could not run", and no shape bought before the feature that needs it exists.

## Declarations: rules by kind, layered by "mine wins"

One `metadata.json` is deserialized twice — into `TableMetadata` (the shape) and into `DeclarationMetadata` (what this endpoint declares about it). The split is not a partition of the file: a key belongs in both types only when it answers two different questions there, and today exactly one does — `table` ("was it written" here, "what is it" there). Otherwise "where do I read this from" comes back. Decided 2026-08.

- **The key of a rule is the KIND of rule, not the field.** `required` is names, `visible`/`computed` are field-to-expression, `inherit` is field-to-source — each kind carries its own shape, so a parameter has nowhere to be orphaned and a new kind does not widen a shape shared by all of them. The inverse reading (field → bag of its rules) failed twice for that: a bag has a level on which `applyIf` sits guarding nothing.
- **Layering is one sentence — mine wins — and one implementation.** It is asked on two axes (storage under operation, collection under row kind) and both call `RuleMetadata.Merge`. **Lists union**: a list on the layer that speaks about everyone is a statement about all of them, not a default one of them may weaken — a requirement that does not hold for all is written in the wrong layer and belongs moved. Overriding there failed silently; unioning fails loudly, as a validator you can see in the generated template. **Maps override by key**, which is what makes partial commonality writable — two kinds computing `Sum` alike and a third differently is one formula plus one override.
- **The far half of a reference is resolved late, and that is what keeps the bake early.** A rule's own columns are findable the moment the declaration is built; a column of the table the reference points at needs the reference graph, which is linked only after publication because it is cyclic. So `inherit` keeps `Source` as a name and `RefMapBuilder` resolves it when it emits SQL. Resolving it eagerly would push the whole bake past publication and leave the endpoint mutable for one field.
- **Declared and resolved never share a field.** `Forms` is what the author wrote, `BakedForms` what the bake resolved. One collection in both roles cannot tell them apart, and the endpoint hands out whichever was written last — which is how a declared form once reached the generator with no columns looked up at all.
- **The bake rebuilds, never fills.** A node it walks may belong to a different, already published endpoint — an operation that declares no rows or no form of its own gets the storage endpoint's by reference. Writing into what was found would be writing into someone else's declaration, so every level returns a new record (`with`) and there is nothing to write into.
- **It is driven from the shape, so the result is total.** Every collection gets a node and every row set an entry, whether the file mentioned them or not. That is the difference between "nothing was declared" and "nothing is declared", and collapsing the two is what deletes the `if (declared == null)` every generator used to carry.

## System endpoints: behaviour is a type, not a kind

Some addresses are served by the platform itself — no `metadata.json`, no shape, no declaration, no forms. Today exactly one: `/tag/settings`, the dialog that edits the tags of an entity. Decided 2026-08.

- **A third subtype of `EndpointMetadata`, dispatched by type at one seam.** `ReportEndpointMetadata` already established the shape: an endpoint that is not a data endpoint, with its own builder, chosen in `AppMetadataBuilder` by what it *is* — no string travels from the loader to the dispatcher and back to say what this is. A system endpoint is the second entry in that same `if`, not a new mechanism.
- **Not an entry in `TableMetadataDefaults.SystemTable`.** That registry answers "which table stands behind this address" and returns a shape. A behaviour has no shape, and putting it there means fabricating a `TableMetadata` for something that has none — the same category error as a `ColumnType` for something that is not stored.
- **`Kind` is set literally; `EndpointKindOf` is not taught the namespace.** That function answers "what did the FOLDER declare", and no folder declares this. Teaching it would also flip `DeclaresShapeSource`, which would then demand a `table` key from an endpoint that has no file to write one in. The endpoint is recognised before the file is read, so nothing goes looking for a `metadata.json` that was never meant to exist.
- **What the container carries is decided by the second one, not the first.** A report carries `Surface` because it reads a shape it does not own. The tag endpoint carries nothing but its address — and that address is written on the type itself, so the control that opens the dialog and the builder that serves it cannot drift apart.
- **The guards were already there.** `GetNormalEndpointAsync` throws "is not a data endpoint", and `ResolveReferencesAsync` names the type explicitly and keeps throwing on an unknown one. Anything that asks a system endpoint for a shape fails in one place, loudly — which is what makes "no shape" safe rather than merely unwritten.
- **The address obeys the same grammar as everything else.** Two segments — `ParsePath` takes no more, so `catalog/tags/settings` silently loses its tail — and the second one is a noun, because `edit` is already the name of an action everywhere else. Identity rides in the query (`?For=Agent`), which is the one place the platform's usual rule inverts: the path is lower-cased and a single segment cannot address an entity that lives in a two-segment namespace.

## Forms: whole or nothing

A form is per **endpoint**, not per shape — an operation and the document storage behind it are one table and two screens. So `forms` lives in `DeclarationMetadata` and `TableMetadata` stays the shape alone. Decided 2026-08.

- **A declared form replaces the default entirely.** Never merged node by node. A partial override adds a second, invisible question at every node — "is this instead of the default, or on top of it?" — and the file carries no answer to it; the model would keep guessing, per node. A declared form has to be readable on its own, without holding `DefaultFormBuilder`'s rules in your head.
- **Across endpoints it layers by form key, and the value is all-or-nothing.** A form is a tree whose nodes carry no names, so there is nothing inside one to address. An operation declaring its own `edit` still shows the storage's `index`.
- **Declared and default take the same walk.** One bake resolves both against the shape, eagerly, while the endpoint is built — so a form the file got wrong fails the load, where a throw publishes nothing, rather than the first request that opens the page. The lazy path is what let a declared form reach the generator unresolved.
- **The price: the default has to be obtainable as text.** "Whole or nothing" means "type it from scratch" until it can be ejected — the eject is therefore owed, not optional. See ISSUES 3.6.

## Commands: a derived set, plus what the endpoint declares

A command is not content of a screen. The form carries **references only**; the definitions live on the endpoint — which is already how `post` works, so this generalizes a shape the platform has rather than inventing one. Decided 2026-08, at this stage.

- **One namespace per endpoint: platform entries plus author entries.** Platform entries are derived (from the kind, from `traits`, from `post`); author entries are declared. An author name colliding with a platform one is an error — which is how "a standard command may not be overridden" is enforced: a name check in one place, not a rule the form has to carry. An overridable standard name stops meaning anything, and reading a form would no longer tell you what `delete` does.
- **Two questions, two layers.** "Does this command exist for this entity" is the endpoint's — a catalog filled by an integration has no `delete`, and the SQL, the selector and the bar all learn it at once. "Does this screen show it, and in what order" is the form's — a browse dialog may legitimately show less than the index page. Removing a button never expresses the first question; it only hides it on one screen.
- **No ambient hatches.** An author command is composed of declared verbs: navigation or a dialog onto an endpoint that must exist, or a verb the endpoint declares (today exactly one — `post`). A new kind of action enters by the platform declaring a new verb, once, not by each application naming a procedure. So a data-changing action that is not posting is not expressible yet: the verb list is short, and it is short in the right place — it grows by decision.
- **`std: true` on the toolbar node, not a `$std` token in the list.** `$sep` and `$toRight` are items: they render. A splice directive is not an item, and putting it in the same list repeats the very conflation this section removes. The frame keeps a marker internally, at the boundary between the entity commands and the chrome tail (`$sep, Reload [, $toRight, Search]`); the format shows no trace of it.
- **Without `std` the list is literal.** `["sendToBank"]` is one button — the fully authored bar. Wanting the standard commands in another order means writing them all out, which leaves the derivation and hands you the list; the price is visible and proportional. It stays safe because every reference resolves against the endpoint's namespace, so a hand-written bar that drifts from the declaration fails the load instead of rendering a dead button.

Nothing here is left to run time: a reference resolves to an endpoint entry, an entry resolves to a verb and an address that exists.

## Members: a form node shows members, not columns

`fields` stays a list of names. What a name resolves to stops being a `TableColumn` and becomes a `MemberDescriptor` — a column, or something a trait contributes to the record. Today there is exactly one of the second sort: `Tags`, whose rows live in their own table and reach the model as an array. Decided 2026-08.

- **Not a `ColumnType.Tags` on a fabricated column.** `ColumnType` answers "what is stored in this column and how", and it feeds `SqlDataType()` for DDL. Tags is not stored on the table at all, so that member would be an enum value with no SQL type — a hole held shut by nobody ever handing it to the wrong function. Cheaper by three mechanical call sites, and structurally unguarded. The same fabrication had already been removed once, from the old `TableFilters()`.
- **The candidate list is built per form, not by a predicate.** `DeclarationBake.BuildForms` is where the three forms already differ, so that is where "which form sees what" is written: `index` and `browse` get the index columns, `edit` gets the edit columns plus `Tags`. A predicate on `TableColumn` cannot express a member that is not a column, and spreading the trait check across the walk would put the answer in two places.
- **The index grid keeps its splice.** Tags already render as a second line inside the `Name` column — a trait changing how a column draws, which is a different act from a trait adding a member. Making it both would give one thing two places to be.
- **`Tags` is in two namespaces and means different things there.** As a member it is the record's own tags, bound to `{Model}.Tags` and editable; as a filter it is `Parent.Filter.Tags` with candidates from the root recordset. Same word, two entries, two controls — which is precisely why `Filters` stays a property of its own instead of collapsing into `fields`.

## Filters: a namespace on the shape, referenced by the form

A filter list was never a field list — three of the four kinds have no column at all (`Fragment`, `Period`, `Tags`), and only ref filters ever borrowed one. So filters are their own namespace, derived from the shape, and a form carries references into it. Decided 2026-08.

- **The kind is how a filter lands in the WHERE, not what draws it.** The control and the `DataType` follow from the kind. Anything that is not equality on a column of the table is a new kind here, once — and then the namespace, the SQL and the panel learn it together. A boolean Yes/No/All is one enum member plus one line, not a new concept.
- **Three consumers that do not see each other: the index SQL, the CollectionView's `FilterDescription`, and the taskpad panel.** Only the panel goes through a form, and only as references, so a form can hide a filter but never invent one. The namespace sits on `TableMetadata` because that is where every input is (the kind, the traits, the columns) and because two of the three consumers never hold a declaration. It moves to `DeclarationMetadata` the day a filter becomes declarable, the way forms moved — not before.
- **SQL cannot follow a form.** One index procedure serves `index`, `indexpartial` and `browse`, and the last has a form of its own — following a form would mean picking which of the two to believe, or two procedures.
- **No collision check between platform names and column names.** It was tried and removed: the real collision surface is the SQL parameter space (`@Fragment`, `@From`, `@Order`, `@Offset`, …), nothing has ever checked it, and guarding two names of ten inside the namespace builder was worse than guarding none — it read as an invariant while being disabled by reordering two lines. A ref column named `Period` gives two controls bound to one Filter property, which is visible on the first page load.

## Skills as spec: a firewall between two instances

Skills (stubs for the application developer building on the platform) are a **contract for the target state**, not instructions to execute. Their value is that they work for a reader who **cannot see the implementation**. Hence two roles in different contexts:

- **This repo (knows the implementation)** — I read skills **read-only, never edit them here**. Found friction → **build an anchor in the platform** so the skill's promise becomes true; don't touch the spec text. If the text itself looks off, that's an **observation for the blind instance**, not my edit (my judgment of the skill's self-sufficiency is compromised by knowing the implementation — an author doesn't grade their own exam).
- **The blind instance (knows nothing about the implementation)** — in a separate context, tries to assemble a real application from the skill + the platform's anchors. Where it stumbles is the signal; **skill edits belong to it**. Only a reader who genuinely can't see the implementation can judge whether the skill stands on its own.

Skills live outside `.claude/skills/` (they're read, not invoked) — in the root `SKILLS/` folder: local read-only junctions to the canon repos, gitignored. One canon stub: `a2v10-skill` — the whole platform, metadata-driven layer included. (`a2v10-md-skill` turned out to be a development artifact; decided 2026-06 to discard it — retirement in progress, don't build on it.) The junction points at the repo's **`skill/` subfolder only** — the published surface; the authoring/dev part of the repo is structurally out of reach, so the firewall is enforced by the setup rather than by my behavior. Entry point: `SKILLS/<repo>/SKILL.md`. I only need read access; glob to discover.

Junction wiring (local, not portable — recorded so it can be rebuilt, not for reading):
- `SKILLS/a2v10-skill` → `c:\Claude\a2v10-skill\skill`
- `SKILLS/a2v10-md-skill` → `c:\Claude\a2v10-md-skill\skill` (still wired while retirement is in progress)

Recreate after loss (no admin needed): `mklink /J SKILLS\<name> c:\Claude\<name>\skill`.

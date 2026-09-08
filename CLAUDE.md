# A2v10.Core

A .NET platform for enterprise web applications (author — Oleksandr Kukhtin); NuGet packages converging on `A2v10.Infrastructure`.

The core is an end-to-end **data-binding engine** (IDataModel: SQL ↔ C# ↔ reactive Vue), not the metadata layer. `_data/*` → `DataService` → `IDbContext.LoadModelAsync` builds a hierarchical IDataModel from a procedure's recordsets by naming conventions; `VueDataScripter` bakes it into a self-contained Vue component; Save posts it back via a TVP.

`Platform/A2v10.Metadata` — an **active** declarative ERP/scaffolding layer over that core, not a stub. From `TableMetadata` (Catalog/Document/Operation/Journal/Details) it either renders pages at runtime (SQL generated on the fly, same IDataModel pipe) or generates sources (model.json/vxaml/ts) and DDL.

---

> A durable artifact of **decisions**, not documentation. Intent lives here; `.cs` is its compile target. Decisions go here, under git — not into memory (`~/.claude`).

## Primary goal: an LLM-aware platform

LLM-aware through **small, relative add-ons**: a simple decision fork, real applications built fast and cheap. Not "add AI" — **trust is structural**, a property of the loop rather than of the model: drift visible and cheap to catch ⇒ safe in real code.

1. **A tight, authoritative feedback loop.** Validate + instantiate + result-JSON in ~1-2ms. **Loop first, facade second** — without a ground-truth beam an LLM-aware DSL is a DSL the model drifts across beautifully.
2. **High decision density at the surface.** The platform absorbs boilerplate decisions; only the domain "what" is left on top.
3. **Frozen judgment inside the platform.** A good cut baked in; an LLM working inside inherits it free.

### The axis: bounded vs ambient (NOT declarative vs imperative)

The danger is **ambient authority** — code reaching for what is not in its signature (DB, file, globals, clock, network). Imperative code is fine in any amount **inside a box**: typed in/out, zero ambient authority, body unread, verified at the seams. The author's imperative code already sits behind such interfaces, so the expensive part is done. Extending: a new capability arrives as a box with declared ports, never as an ambient hatch.

### Where the risk remains

Boxes are safe by construction; only **composition** can lie — a wrong graph of correct boxes, which is where the human sits. So the beam (#1) verifies the **assembled result at the assembly seam**, not individual boxes.

## Working method

Little code, many decisions — the reverse of typical enterprise. No legacy because of the author's cuts, not because of generation.

- **Code is a compile target.** Wrong output → fix the intent (this file / a test), regenerate. Never patch `.cs` to silence a symptom. Correctness is behaviour (test green, expected JSON), not the look of generated code.
- **Make decisions, don't hand them back.** Obvious cut → cut, and say what was cut. No menu of two tracks. Default **"don't build"** over "build the general case"; typical, not exhaustive; defer dead code.
- **Don't touch working code you weren't pointed at.**
- **Probe, don't theorize.** Beam first (build / test / validate+JSON), assertion second.
- **Don't extend to the uncomfortable end for beauty** — that is agreement-as-performance. Restraint is the judgment being asked for.

Labor: the human designs boxes and holds composition/intent; the LLM fills boxes with imperative code; the platform guards the frame.

## The `a2` CLI: shape of the command tree

`Tools/A2v10.Cli` (package `A2v10.CLI`, command `a2`) is the model's feedback loop — the beam of #1, navigated blind, so its shape is a decision. Decided 2026-08.

- **A first-level command is never a verb.** Top level = areas (`app`, `db`, `endpoint`, `meta`, `view`); verbs are leaves. Break it once and every later capability has two homes (`validate view` vs `view validate`) — placement stops being derivable and the model guesses. A cross-cutting action is a leaf repeated per area, not a fifth area.
- **A group is named by platform role, not file format.** `view validate`, not `xaml validate`: `view` is the platform's word (the `view:` key of `model.json`), `.vxaml` is a per-project convention, and one xaml format carries two unrelated element sets (views, report templates) — a format-named group promises both, delivers one.
- **Area choice is free for the model; output shape is not.** Measured: it never picks the wrong area for a file it just edited. So no token-saving facades (dispatch by extension, one universal `validate`); that budget goes to the result JSON — one shape per command, findings distinguishable from "the tool could not run", no shape built before the feature needing it.

## Declarations: rules by kind, layered by "mine wins"

One `metadata.json`, deserialized twice: `TableMetadata` (the shape), `DeclarationMetadata` (what this endpoint declares about it). Not a partition of the file — a key sits in both only when it answers two different questions, and today one does: `table` ("was it written" / "what is it"). Otherwise "where do I read this from" comes back. Decided 2026-08.

- **A rule is keyed by KIND, not by field.** `required` = names, `visible`/`computed` = field→expression, `inherit` = field→source; each kind carries its own shape, so a parameter cannot be orphaned and a new kind widens nothing shared. Field→bag-of-rules failed twice on this: a bag has a level where `applyIf` guards nothing.
- **Layering is one sentence — mine wins — and one implementation** (`RuleMetadata.Merge`, on both axes: storage under operation, collection under row kind). **Lists union**: a list on the layer that speaks about everyone states something about all of them; a requirement that does not hold for all is written in the wrong layer. Overriding failed silently, unioning fails loudly — as a validator visible in the generated template. **Maps override by key**, which makes partial commonality writable: two kinds computing `Sum` alike and a third differently = one formula plus one override.
- **The far half of a reference resolves late; that is what keeps the bake early.** A rule's own columns are findable as the declaration is built; a column of the target table needs the reference graph, linked only after publication because it is cyclic. So `inherit` keeps `Source` as a name and `RefMapBuilder` resolves it when emitting SQL. Eagerly would push the bake past publication and leave the endpoint mutable for one field.
- **Declared and resolved never share a field.** `Forms` = written by the author, `BakedForms` = resolved by the bake. One collection in both roles hands out whichever was written last — how a declared form once reached the generator with no columns looked up.
- **The bake rebuilds, never fills.** A node it walks may belong to another, already published endpoint (an operation with no rows or form of its own gets the storage's by reference), so writing into it would write into someone else's declaration. Every level returns a new record (`with`).
- **Driven from the shape, so the result is total.** Every collection gets a node, every row set an entry, mentioned in the file or not — the difference between "nothing was declared" and "nothing is declared", and what deletes the `if (declared == null)` every generator carried.

## System endpoints: behaviour is a type, not a kind

Addresses served by the platform itself: no `metadata.json`, no declaration, no generated forms — screen, data and save are code. Two: `/tag/settings` (edits an entity's tags) and `/operation` (registry of operation codes, one `browse`). Decided 2026-08.

- **A subtype of `EndpointMetadata`, turned into a builder at one seam.** Shape set by `ReportEndpointMetadata`: not a data endpoint, own builder, chosen by what it *is* — no string travels loader → dispatcher → back. The seam is `ModelBuilderFactory.BuildAsync`, the only place knowing the kinds; everything above holds one `IModelBuilder` and asks.
- **The builder contract refuses by default; only `Path` is required.** Every action on `IModelBuilder` defaults to throwing `'{action}' is not supported for '{path}'`, so a builder declares only what it serves. Not an abstract base with throwing virtuals: a default interface member is reachable through the interface and never through the class, so a concrete builder's surface stops claiming six things it cannot do. `Path` is undeclinable — every refusal is worded with it.
- **Not an entry in `TableMetadataDefaults.SystemTable`.** That registry answers "which table stands behind this address" and returns a shape; a behaviour has none, so an entry means fabricating a `TableMetadata` — the same category error as a `ColumnType` for something not stored.
- **`Kind` is set literally; `EndpointKindOf` is not taught the namespace.** It answers "what did the FOLDER declare", and no folder declares this. Teaching it would flip `DeclaresShapeSource`, demanding a `table` key from an endpoint with no file to write one in. The endpoint is recognised before the file is read.
- **What the container carries is decided by the second case, not the first.** The tag dialog carries only its address, written on the type, so the control opening it and the builder serving it cannot drift. The operation registry carries a **shape**, because it is pointed at — every document's `operation` column resolves to it. Hence `IRefTarget`: the two members (`Storage`, `Path`) every reader of `RefTable` asks for, so widening the field moved no call site. A report does not implement it — it owns no data to point at.
- **A code-declared table ≠ a system endpoint.** `TagsTable()` and `OperationsTable()` stay; the deploy and the builders read them. System means *the endpoint* is described nowhere. Once both addresses had their own types, `TableMetadataDefaults.SystemTable` had nothing left to answer: it existed only to give a code-declared table an address inside the file pipeline, and a system endpoint gives it one directly.
- **Leaving the generated pipeline removes a class of bug, not just a page.** `ColumnType.Operation` means both the identity of an operation (key of `doc.[Operations]`) and a reference to one; `IsRef` says yes to both. The generated index met only the second until asked to render the registry, and threw on its own key. Not patched — the page left the pipeline and the ambiguity became unreachable.
- **The guards were already there.** `GetNormalEndpointAsync` throws "is not a data endpoint"; `ResolveReferencesAsync` names the type and throws on an unknown one. Asking a system endpoint for a shape fails in one place, loudly — what makes "no shape" safe rather than merely unwritten.
- **The address obeys the same grammar as everything else.** Two segments (`ParsePath` takes no more, so `catalog/tags/settings` silently loses its tail); the second is a noun, because `edit` names an action everywhere else. Identity rides in the query (`?For=Agent`) — the one place the usual rule inverts: the path is lower-cased and a single segment cannot address an entity in a two-segment namespace.

## Forms: whole or nothing

A form is per **endpoint**, not per shape — an operation and its document storage are one table and two screens. So `forms` lives in `DeclarationMetadata`; `TableMetadata` stays the shape alone. Decided 2026-08.

- **A declared form replaces the default entirely**, never node by node. A partial override adds an invisible second question at every node ("instead of, or on top of?") that the file cannot answer, so the model guesses per node. A declared form must be readable without holding `DefaultFormBuilder` in your head.
- **Across endpoints it layers by form key; the value is all-or-nothing.** A form is a tree of unnamed nodes — nothing inside to address. An operation declaring its own `edit` still shows the storage's `index`.
- **Declared and default take the same walk**, eagerly, while the endpoint is built: a wrong form fails the load, where a throw publishes nothing, instead of at the first request. The lazy path is what let a declared form reach the generator unresolved.
- **Price: the default must be obtainable as text.** "Whole or nothing" means "type it from scratch" until it can be ejected — the eject is owed, not optional. See ISSUES 3.6.

## Commands: a derived set, plus what the endpoint declares

A command is not screen content. The form carries **references only**; definitions live on the endpoint — as `post` already does. Decided 2026-08, at this stage.

- **One namespace per endpoint: platform entries plus author entries.** Platform entries derive from the kind, `traits`, `post`, `printForms`; author entries are declared. A collision is an error — that is how "a standard command may not be overridden" is enforced, by one name check rather than a rule the form carries. An overridable standard name stops meaning anything: reading a form would no longer tell you what `delete` does.
- **Two questions, two layers.** "Does this command exist for this entity" → the endpoint (a catalog filled by an integration has no `delete`; SQL, selector and bar learn it at once). "Does this screen show it, in what order" → the form (a browse dialog may show less than the index). Removing a button only ever hides it on one screen.
- **No ambient hatches.** An author command composes declared verbs: navigation or a dialog onto an endpoint that must exist, or a verb the endpoint declares (today one — `post`). New kinds of action enter by the platform declaring a verb, once, not by each application naming a procedure. So a data-changing action that is not posting is not expressible yet: the verb list is short in the right place, and grows by decision.
- **`std: true` on the toolbar node, not a `$std` token in the list.** `$sep` and `$toRight` render; a splice directive does not, and putting it in the same list repeats the conflation this section removes. The marker stays internal, at the boundary between entity commands and the chrome tail (`$sep, Reload [, $toRight, Search]`).
- **Without `std` the list is literal.** `["sendToBank"]` is one button. Reordering the standard set means writing it all out — the price is visible and proportional. Safe because every reference resolves against the endpoint's namespace: a drifting hand-written bar fails the load instead of rendering a dead button.

Nothing is left to run time: a reference resolves to an endpoint entry, an entry to a verb and an address that exists.

## Members: a form node shows members, not columns

`fields` stays a list of names; a name resolves to a `MemberDescriptor` — a column, or something a trait contributes. Today one of the second sort: `Tags`, rows in their own table, reaching the model as an array. Decided 2026-08.

- **Not a `ColumnType.Tags` on a fabricated column.** `ColumnType` answers "what is stored in this column and how" and feeds `SqlDataType()` for DDL; tags are not stored on the table, so that member is an enum value with no SQL type — a hole held shut only by nobody handing it to the wrong function. Cheaper by three mechanical call sites, and structurally unguarded. The same fabrication was already removed once, from the old `TableFilters()`.
- **The candidate list is built per form, not by a predicate.** `DeclarationBake.BuildForms` is where the three forms already differ: `index`/`browse` get index columns, `edit` gets edit columns plus `Tags`. A predicate on `TableColumn` cannot express a non-column member, and spreading the trait check across the walk puts the answer in two places.
- **The index grid keeps its splice.** Tags render as a second line inside `Name` — a trait changing how a column draws, a different act from a trait adding a member. Both would give one thing two places to be.
- **`Tags` sits in two namespaces with two meanings.** Member: the record's own tags, bound to `{Model}.Tags`, editable. Filter: `Parent.Filter.Tags`, candidates from the root recordset. Two entries, two controls — why `Filters` stays its own property instead of collapsing into `fields`.

## Filters: a namespace on the shape, referenced by the form

Never a field list: three of four kinds have no column (`Fragment`, `Period`, `Tags`), only ref filters borrowed one. So filters are their own namespace, derived from the shape; a form carries references into it. Decided 2026-08.

- **The kind is how a filter lands in the WHERE, not what draws it.** Control and `DataType` follow from the kind. Anything that is not equality on a column of the table is a new kind, once — then namespace, SQL and panel learn it together. A boolean Yes/No/All is one enum member plus one line.
- **Three consumers that do not see each other:** the index SQL, the CollectionView's `FilterDescription`, the taskpad panel. Only the panel goes through a form, and only as references — a form can hide a filter, never invent one. The namespace sits on `TableMetadata` because that is where the inputs are (kind, traits, columns) and because two consumers never hold a declaration. It moves to `DeclarationMetadata` the day a filter becomes declarable, as forms did — not before.
- **SQL cannot follow a form.** One index procedure serves `index`, `indexpartial` and `browse`, and the last has its own form: following a form means choosing which to believe, or writing two procedures.
- **No collision check between platform names and column names.** Tried and removed: the real collision surface is the SQL parameter space (`@Fragment`, `@From`, `@Order`, `@Offset`, …), which nothing has ever checked, so guarding two names of ten inside the namespace builder was worse than guarding none — it read as an invariant while being disabled by reordering two lines. A ref column named `Period` gives two controls on one Filter property, visible on the first page load.

## Posting by procedure: a box with two ports

Some postings are not a column mapping — a cost calculation reads the journal it is about to write, which no `document`/`row` pair expresses. So `post` has a second spelling: `sql: { post, unpost }` plus `journals`. Not the ambient hatch Commands refuses: a procedure is imperative code in a box, and it is a box because its ports are declared here rather than reached for. Decided 2026-09.

- **The journals are declared even though the procedure writes them.** Nothing can read them out of a procedure, and two consumers need them: the transactions dialog, and the unpost when unauthored. A journal named here carries provenance (`ColumnType.Document`) whoever writes it, keeping "what did this document post" answerable without reading SQL. An unverified promise — the whole price of the second spelling, named exactly, in one place.
- **`unpost` is optional; the derived delete is the default.** A procedure that wrote the provenance is undone by the same delete as any posting. Write it only for an inverse that is not a delete — a reversal keeping the rows.
- **Two ports, `@Id` and `@UserId`.** Anything more is a fact taken without being declared; the rest is read from the document handed in. The line between "imperative inside a box" and ambient authority is drawn at the parameter list.
- **The transaction and the `Done` flag stay the platform's.** `begin tran` → `update Done` (which IS the lock) → `exec` → `commit`. So the procedure never meets a second caller and cannot be asked to be idempotent, and the document is already in its target state when it runs. It does not own the transaction: `commit` there only decrements `@@TRANCOUNT`, `rollback` kills the outer one and makes the platform's commit fail far from the cause — so failure is a `throw`.
- **One `sql` entry, never mixed with mapped legs.** Half-platform, half-procedure cannot say which wrote a row, and its unpost has no order to undo in; two procedures bury the same answer in a file. Two are written as one calling two, where the order is visible in code. Refused at load, in one check.
- **The procedure name is verified nowhere.** Deliberately: the first posting exercises it and fails loudly, unlike a mapping typo sitting under a button nobody presses while developing an index page. Checking it costs a deploy-time round trip over a walk that collects only shape-owning endpoints, and buys a message the database already gives.
- **Nothing outside changes** — commands, `$invoke('post')`, the buttons bound to `Done`, the dialog. The test that the seam was cut right: `PostStatements` hands out the BODY of the two statements; the frame is written once.

## Transactions: a projection of `post`, not a screen

`ShowTrans` shows what one document put into the journals, all of it derived: tabs = the journals `post` targets, rows = found by the provenance column posting already requires, columns = the journal's own. No form, no `forms` key — nothing an author could say that `post` has not. Decided 2026-08.

- **Its own projection, not the journal's index embedded.** Rejected: `Include` on the journal's `indexpartial` with `?Document=<id>` — no SQL at all, page and filter exist, paging and sorting free. It fails on what this screen *is*: the projection drops what the document fixes, and that difference belongs to the pair (document, journal), not to the journal. On the journal side it needs a flag saying "I am embedded" — a screen whose appearance depends on who opened it, ambient authority wearing a parameter.
- **A column the document FIXES carries no information here.** `Document`, `DocumentType`, `Date`, `Operation` — dropped by `ColumnType`, not by the post mapping. "Filled from the header" is a different question and answers wrongly: a journal's `Agent` also comes from the header, is equally constant, and is exactly what the dialog is opened to read. The wider rule — any column pinned by an equality filter — was refused: on the journal's own index the filter is visible and clearable, and a column vanishing as you pick one is a worse screen. Right here only because the filter is fixed by context, with no control to change it.
- **A journal is called by its FOLDER — not `Model`, not `Table`.** One word names the array, the row type, the tab's switch value and the localization key; drift between them is a tab matching no case. `Model` is a shape several journals may share, so `Transaction` would name two different arrays of two different column sets in one model. `Table` is where rows are stored (`jrn.StockJournal`) and renames only through a migration. The folder is what the address calls the thing, and what `TableMetadata.SetDefaults` PascalCases into the default `Model`.
- **The ref map spans several tables.** The WHERE lives on `RefMapItem`, not in a conditional over `SourceTable.Kind` inside the insert generator: one `@map`, one resolve per target, so a catalog three journals point at is fetched once. The columns are a parameter for the same reason — a map resolves what a recordset SENT, and taking them from the table refetched the journal's `Document` on every row of the document you are standing on.
- **Slots come from the widest source, not the first.** Naming `@map`'s columns after the first source and reaching past its end for the rest is an index out of range: unreachable while a header out-numbered its details, reachable the moment several tables are unioned, where nothing orders them.
- **Posting commands exist when the endpoint posts.** `Post`, `UnPost`, `ShowTrans` — one group with its own leading separator, emitted only for a declared `post`, which is why the default form builder is handed the declaration and not only the shape. Without it a document that never posts carries three buttons into a screen with nothing to show.

## Print forms: paper under one act

A blank is not a kind — no window, no endpoint, no folder. `printForms` is a flat list on the endpoint's declaration: path to a file in a foreign format (a Workbook today) plus a title. The platform reads exactly one section of that file, `Model`, and hands the rest to a report engine. Decided 2026-08.

- **Per endpoint, never inherited from `storage`.** A blank is paper under an ACT: two operations over one table print different papers, and the storage has no act. So `printForms` is not named in `MergeDeclaration` — the same silence, for the same reason, as `post`.
- **No trait.** `TableTrait.Print` is gone: a trait lives on the SHAPE, and one shape shared by several operations cannot answer whether this one prints. A non-empty `printForms` is the whole answer; SQL, toolbar and menu learn it together.
- **One command; the screen is a parameter.** `Print` sits on both toolbars, `CommandScope` tells the builder which — the card hands it the record, the grid the selected row. Not two entries in `EntityCommandType`: printing is one act with two argument sources, and that namespace answers what the entity can DO. Not a lookup of the current action either — a button drawing differently depending on the caller is the hardest drift to see, so the two statically-known call sites pass it.
- **Depth is `RefId` + chained Maps, not dotted names.** A dotted property name carries exactly ONE type name (`FieldInfo`, `x[1]`) and the loader refuses a third segment (`DataModelReader.ProcessComplexMetadata`), so `Agent.TaxDepartment.Name` has no spelling there — and inventing one would DOUBLE the intermediate type under a name no other recordset uses. A Map row goes through `ProcessFields` like any other, so a `RefId` inside one makes the next placeholder and the next Map fills it: depth falls out of the mechanism. Order-free (`RefMapper` keeps a forward definition), a null reference yields an empty object, one agent behind fifty rows is fetched once.
- **One map per TABLE, over the union of every path onto it.** Two paths of a finite tree reach one table at different depths (a row's `Unit`, its item's `Unit`), so the map is built to a fixed point and regrouped each round. Emitting a table when first reached dropped the later path silently: a short map and a blank cell, no error.
- **Every column of every predicate is qualified.** A container's predicate is reused verbatim inside the subquery below it, and an unqualified name binds to the innermost scope that HAS such a column — so a column the inner table lacks becomes a correlated reference to the outer one. Silent wrong answer, not an error. Hence an alias per recordset, and a map's alias fixed to its table so the fixed point settles.
- **A collection is addressable under both names.** The declared key of `details` is every row; the composed name of a kind (`ServiceRows`) is that kind alone, own type and filter; a blank may use both. The one place the usual refusal of composed names inverts (`TableMetadata.ComposedRowSetHint`): the layout binds a `Range` to an ARRAY, and a scope-plus-kind pair has no single name.
- **`Id` is implicit, everything else is written.** Implicit only what the mechanism requires — nothing is addressable without `Id`, and both the Map merge and the master link key on it. `RowNo` is not: the paper wants it, the machine does not, and a second exception leaves a list to memorise.
- **`?Form=` names a declared blank, and is required.** Never a path from the client — that is a request for an arbitrary file. No first-blank default: a request that does not say what to print asks for nothing, and answering with whichever came first hands back a plausible wrong document.
- **The layout is NEVER parsed** — not to build the fetch, not to check it: a cell calls functions and reads fields inside JS, so no reading of it could answer what to fetch. Price, real and accepted: a cell reading what `Model` does not carry renders empty and says nothing. Seen on the first printed page.
- **Not baked into the declaration.** Tempting, because forms are — but there the prize is failing at load, here it is negative: a typo in a peripheral blank would take down the document's index and card. What is left is caching, which one human click does not pay for. The viewer's host page loads only the record's `Id`; the blank is read once, by the report handler, when rendered.

## Autonums: a column and a key, never a trait

A document's number is an ordinary field the user may correct; the platform fills it on insert when empty. Which numbering the endpoint draws from is `autonum` on its declaration; what the numbering IS — pattern, period — is declared once, at `/autonum`. Decided 2026-09.

- **No trait; nothing added to `TableTrait`.** A column of `ColumnType.Autonum` is the whole answer to "is this numbered", as a non-empty `printForms` answers "does this print" — `TableTrait.Print` was removed for it. A trait would also have to ADD the column, and the platform adds columns only where it reads and writes them itself (`Parent`, `Folder`); a number is user data, written in `fields` next to `Name`. The author names it, the platform finds it BY TYPE, as with `RowKind` and `Operation`. Two such columns in one table is a throw: which one gets the number must not be a guess.
- **The check runs one way only.** `autonum` declared with no column of that type — throw. The reverse is legitimate: a column with no `autonum` is numbered by hand. The json schema's "required exactly when the storage declares a field of type Autonum" claims one direction too many.
- **Assigned on insert if empty; the save frame is untouched.** The number travels in the TVP and updates like any field; the platform fills only the empty one. No unique index: two operations over one storage may sit on different autonums, and their numbers legitimately meet in one table.
- **Gaps are accepted, not fought.** Numbering is never gapless in practice, and buying it costs the save an outer transaction plus a lock held through the whole save, details merges included. The number is issued on its own; a rollback eating one is the price.
- **The pointer is enum-shaped, not storage-shaped.** `"autonum": "waybill"` resolves to a VALUE inside `/autonum`, not to an endpoint — so the check has the shape `CheckLiteralInitials` already uses, at load, with no round trip. The address is the storage form (`SetDefaults` yields `/{schema}` on an empty table segment), and the key layers as everything else: an operation may number from its own, silence inherits the storage's.
- **The key in that file is `autonums` — zero new words.** Not `values`: the record is (id, name, pattern, period), and one key whose shape depends on the endpoint kind is two questions under one name. Not `series` (batch accounting wants that word), not `sequences` (`SQ_<table>` under every `Id` is already one), not `numerator` (a Russism; in English it is the top half of a fraction). `ColumnType.Autonum`, the declaration key, the folder and `doc.Autonums` already say this word. It reads by the enum's rule: address names the concept, key names the elements, pointer names one element.
- **The pattern is in the file; only the counter is data.** `{p}` is substituted from the company row at issue time — a substitution, not a reason to keep the template in the database. Declared, it leaves the deploy nothing to overwrite and the two sides nothing to drift on; the counter table holds counters only. A human-edited pattern is a different decision, arriving with the screen that edits it.
- **No `void` on an entry.** On an enum value it withdraws a candidate; nobody picks an autonum at run time — a file names it. A key deleted from the file keeps its row: the deploy merges without a delete arm (as `MergeOperations` does), and documents carry the numbers it issued, so the counter must not restart.
- **The screen over the counters is not built.** An enum has no screen of its own — values are read through the columns pointing at them. A screen here would exist only for what the file does not hold: the current counter per period. Until wanted, `/autonum` is a file, a table, its rows, and the code issuing a number.
- **The file declares the numberings; its three names default.** One registry at one address, so schema, table and model default (`SetDefaults`) — written, they win. Rows land in the document schema: a namespace of its own is the ADDRESS, never where rows live. `ToEndpointKind` learns the folder because the kind must be set; `EndpointKindOf` deliberately does not — it answers "does this folder declare where its shape comes from", and this one does not, the same silence as `tag` and `operation`. The `schema == "autonum"` skip in `AllElementsMetadata` is removed: it skipped the one thing that now has to be deployed.

## Links: part and belonging, and one naming rule

Two relations wore one word. A details row and a tag entry are **part of** the record they hang under: platform-emitted, dead when it dies, never shown, no target of their own. A subordinate catalog record **belongs to** an entity: own save, own card, referenceable, several such links per table, each with a declared target. `Owner` named the first and was the right word for the second. Decided 2026-09.

- **`Master` is the part, `Owner` the belonging; only the first is built.** They share "an immutable link to a parent" and nothing else: opposite lifecycle (one dies with the record, one outlives it because documents point at it), opposite data path (one is loaded and saved inside the master's model, one saves itself), FK target derived in one case and declared in the other. One type would mean "target derived OR declared depending on where the table sits" — the "where do I read this from" question again.
- **Told apart by shape, not by remembering which word is which.** `Master` carries no `target` (only `SqlDbGenerator.DeployTables` knows what it points at); `Owner` requires one. One field answers it while reading any file.
- **The rename was total — the whole reason it was done now.** A half-rename leaves one word with two meanings, exactly what was being removed: `Constants.FieldNames.Owner` is gone, tag entries included, and `Owner` survives only as the comment reserving it. Nothing is deployed on this layer yet, so the cost was zero; the same rename after the first installed base is a migration for everyone. The word goes to belonging because that is 1C's «Владелец», and a migration is the only reason any of this is being built.
- **A link column is named for what it POINTS AT.** `[Document]` on a waybill's rows, `[Agent]` on an agent's tag entries, `[Agent]` on a ref — one rule for every link instead of "refs by target, this one by relation". The name stops being a marker, so `PostStatements` and `RefMapBuilder`, which spelled it literally, now find the column **by its type**, as the platform does everywhere else.
- **`MasterField` is held, not derived; exactly two factories write it.** The hanging table alone does not know its master, so the name is set where the master is in hand — `SetDetailDefaults` and `CreateTagEntriesTable`, beside the `Table` built from the same `Model`. An empty string is a real answer: hangs under nobody. Price, paid knowingly: the tag SQL spells `Table.Model` at the reader sites rather than asking the entries table — the trade `TagEntriesTableName(Table.Model)` already makes one line above.
- **Neither word can be declared in a file.** `owner` left the json schema's `ColumnType` enum and `master` was not added: the platform emits this column, and an author reaching for the familiar word gets a schema error instead of a silent second column. When `Owner` exists, the load refuses it on a details table for the same reason.
- **Belonging is designed, not built.** Beyond the type it needs one form rule (a master is shown and editable while the record is new, gone after the first save — the creation context supplies one of several, the rest have nowhere else to come from) and one new capability: a reference to a subordinate catalog whose candidates are filtered by the owner of the same record. That capability is the whole cost, and deferring leaves no trace in data — an import writes rows directly, only a user typing a new document meets a picker. One owner or several at once is deliberately not asked: every consumer (seeding, embedded list, code series, immutability) reads the same either way, so a check would be an assertion nobody reads and wrong on half the real tables.

## Seed: what the database is told about itself

`a2meta.Tables` / `a2meta.Columns` are the deploy's own picture of the schema, and the referrer set is read from there rather than from metadata — in release there is no `AllElementsMetadata` walk, so SQL is the only thing that can answer a question about the whole application. Decided 2026-09.

- **A fact enters the seed only when SQL must answer it for the whole application at once.** Referrers and the part-of edge (`master_schema`/`master_table`/`master_column`) qualify; `kind`, `traits`, `model` do not. Without the line the seed drifts into a copy of the files.
- **The edge comes from the walk that builds the foreign keys** (`SqlDbGenerator.DeployTables`), so seed and DDL cannot disagree about what hangs under what. Written only where a link column exists: the walk hands a master to the autonum counters too, and those are keyed by a code with no link at all.
- **It answers "used" correctly for hanging tables.** A details row has no `Void`; the record is the master, so `DbRemove` joins to it. A row of a voided document stops holding what it points at.
- **Read with a left join, never inner.** A missing `Tables` row must not DROP a referrer — that is a permitted delete of something already referenced. `GetFkReferrers` aliases its columns to the property names of `TableReferrer`: the list loader matches ordinally, so a lower-case one fills nothing and the row arrives empty instead of failing.

## Skills as spec: a firewall between two instances

Skills (stubs for the application developer building on the platform) are a **contract for the target state**, not instructions to execute. Their value is that they work for a reader who **cannot see the implementation**. Two roles, two contexts:

- **This repo (knows the implementation)** — skills are read-only, never edited here. Friction found → **build an anchor in the platform** so the skill's promise becomes true; don't touch the spec text. If the text looks off, that is an **observation for the blind instance**: my judgment of the skill's self-sufficiency is compromised by knowing the implementation.
- **The blind instance (knows nothing)** — in a separate context, assembles a real application from the skill plus the platform's anchors. Where it stumbles is the signal; **skill edits belong to it**.

Skills live outside `.claude/skills/` (read, not invoked) — in the root `SKILLS/` folder: local read-only junctions to the canon repos, gitignored. One canon stub: `a2v10-skill`, the whole platform, metadata-driven layer included. (`a2v10-md-skill` was a development artifact; decided 2026-06 to discard it — retirement in progress, don't build on it.) The junction points at the repo's **`skill/` subfolder only**, the published surface, so the firewall is enforced by the setup rather than by my behavior. Entry point: `SKILLS/<repo>/SKILL.md`; glob to discover.

Junction wiring (local, not portable — recorded so it can be rebuilt):
- `SKILLS/a2v10-skill` → `c:\Claude\a2v10-skill\skill`
- `SKILLS/a2v10-md-skill` → `c:\Claude\a2v10-md-skill\skill` (still wired while retirement is in progress)

Recreate after loss (no admin needed): `mklink /J SKILLS\<name> c:\Claude\<name>\skill`.

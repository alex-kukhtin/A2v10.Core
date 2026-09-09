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

## Where the decisions live

A decision sits next to the code it compiles into. This file keeps what holds for the whole repo — goal, axis, method — and a map; an area file is read deliberately, by the line that names it. No `@`-imports: they would inline eagerly and this file would be the same size again.

- [Platform/A2v10.Metadata/CLAUDE.md](Platform/A2v10.Metadata/CLAUDE.md) — Declarations: rules by kind, layered by "mine wins" · System endpoints: behaviour is a type, not a kind · Forms: whole or nothing · Commands: a derived set, plus what the endpoint declares · Members: a form node shows members, not columns · Filters: a namespace on the shape, referenced by the form · Posting by procedure: a box with two ports · Transactions: a projection of `post`, not a screen · Print forms: paper under one act · Autonums: a column and a key, never a trait · Links: part and belonging, and one naming rule · Seed: what the database is told about itself
- [ReportEngines/CLAUDE.md](ReportEngines/CLAUDE.md) — Report expressions: a path, or JS with one scope · Report images: one value, one resolver
- [Tools/A2v10.Cli/CLAUDE.md](Tools/A2v10.Cli/CLAUDE.md) — The `a2` CLI: shape of the command tree

Open issues and design debts of the metadata layer: [Platform/A2v10.Metadata/ISSUES.md](Platform/A2v10.Metadata/ISSUES.md).

## Skills as spec: a firewall between two instances

Skills (stubs for the application developer building on the platform) are a **contract for the target state**, not instructions to execute. Their value is that they work for a reader who **cannot see the implementation**. Two roles, two contexts:

- **This repo (knows the implementation)** — skills are read-only, never edited here. Friction found → **build an anchor in the platform** so the skill's promise becomes true; don't touch the spec text. If the text looks off, that is an **observation for the blind instance**: my judgment of the skill's self-sufficiency is compromised by knowing the implementation.
- **The blind instance (knows nothing)** — in a separate context, assembles a real application from the skill plus the platform's anchors. Where it stumbles is the signal; **skill edits belong to it**.

Skills live outside `.claude/skills/` (read, not invoked) — in the root `SKILLS/` folder: local read-only junctions to the canon repos, gitignored. One canon stub: `a2v10-skill`, the whole platform, metadata-driven layer included. (`a2v10-md-skill` was a development artifact; decided 2026-06 to discard it — retirement in progress, don't build on it.) The junction points at the repo's **`skill/` subfolder only**, the published surface, so the firewall is enforced by the setup rather than by my behavior. Entry point: `SKILLS/<repo>/SKILL.md`; glob to discover.

Junction wiring (local, not portable — recorded so it can be rebuilt):
- `SKILLS/a2v10-skill` → `c:\Claude\a2v10-skill\skill`
- `SKILLS/a2v10-md-skill` → `c:\Claude\a2v10-md-skill\skill` (still wired while retirement is in progress)

Recreate after loss (no admin needed): `mklink /J SKILLS\<name> c:\Claude\<name>\skill`.

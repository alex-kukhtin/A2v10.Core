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

## Skills as spec: a firewall between two instances

Skills (stubs for the application developer building on the platform) are a **contract for the target state**, not instructions to execute. Their value is that they work for a reader who **cannot see the implementation**. Hence two roles in different contexts:

- **This repo (knows the implementation)** — I read skills **read-only, never edit them here**. Found friction → **build an anchor in the platform** so the skill's promise becomes true; don't touch the spec text. If the text itself looks off, that's an **observation for the blind instance**, not my edit (my judgment of the skill's self-sufficiency is compromised by knowing the implementation — an author doesn't grade their own exam).
- **The blind instance (knows nothing about the implementation)** — in a separate context, tries to assemble a real application from the skill + the platform's anchors. Where it stumbles is the signal; **skill edits belong to it**. Only a reader who genuinely can't see the implementation can judge whether the skill stands on its own.

Skills live outside `.claude/skills/` (they're read, not invoked) — in the root `SKILLS/` folder: local read-only junctions to the canon repos, gitignored. Two canon stubs: `a2v10-skill` (raw/escaped platform) and `a2v10-md-skill` (metadata-driven). Each junction points at the repo's **`skill/` subfolder only** — the published surface; the authoring/dev part of the repo is structurally out of reach, so the firewall is enforced by the setup rather than by my behavior. Entry points: `SKILLS/<repo>/SKILL.md`. I only need read access; glob to discover.

Junction wiring (local, not portable — recorded so it can be rebuilt, not for reading):
- `SKILLS/a2v10-skill` → `c:\Claude\a2v10-skill\skill`
- `SKILLS/a2v10-md-skill` → `c:\Claude\a2v10-md-skill\skill`

Recreate after loss (no admin needed): `mklink /J SKILLS\<name> c:\Claude\<name>\skill`.

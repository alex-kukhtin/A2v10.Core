# A2v10.Cli — decisions

> A durable artifact of **decisions**, not documentation. Intent lives here; `.cs` is its compile target.
> Platform-wide goal and working method: [/CLAUDE.md](../../CLAUDE.md).

## The `a2` CLI: shape of the command tree

`Tools/A2v10.Cli` (package `A2v10.CLI`, command `a2`) is the model's feedback loop — the beam of #1, navigated blind, so its shape is a decision. Decided 2026-08.

- **A first-level command is never a verb.** Top level = areas (`app`, `db`, `endpoint`, `meta`, `view`); verbs are leaves. Break it once and every later capability has two homes (`validate view` vs `view validate`) — placement stops being derivable and the model guesses. A cross-cutting action is a leaf repeated per area, not a fifth area.
- **A group is named by platform role, not file format.** `view validate`, not `xaml validate`: `view` is the platform's word (the `view:` key of `model.json`), `.vxaml` is a per-project convention, and one xaml format carries two unrelated element sets (views, report templates) — a format-named group promises both, delivers one.
- **Area choice is free for the model; output shape is not.** Measured: it never picks the wrong area for a file it just edited. So no token-saving facades (dispatch by extension, one universal `validate`); that budget goes to the result JSON — one shape per command, findings distinguishable from "the tool could not run", no shape built before the feature needing it.

# A2v10.Cli — decisions

> A durable artifact of **decisions**, not documentation. Intent lives here; `.cs` is its compile target.
> Platform-wide goal and working method: [/CLAUDE.md](../../CLAUDE.md).

## The `a2` CLI: shape of the command tree

`Tools/A2v10.Cli` (package `A2v10.CLI`, command `a2`) is the model's feedback loop — the beam of #1, navigated blind, so its shape is a decision. Decided 2026-08.

- **A first-level command is never a verb.** Top level = areas (`app`, `db`, `endpoint`, `meta`, `view`); verbs are leaves. Break it once and every later capability has two homes (`validate view` vs `view validate`) — placement stops being derivable and the model guesses. A cross-cutting action is a leaf repeated per area, not a fifth area.
- **A group is named by platform role, not file format.** `view validate`, not `xaml validate`: `view` is the platform's word (the `view:` key of `model.json`), `.vxaml` is a per-project convention, and one xaml format carries two unrelated element sets (views, report templates) — a format-named group promises both, delivers one.
- **A leaf that writes never overwrites.** `meta materialize` writes files a human edits from then on; a file already there is a refusal naming it, and the call writes all or nothing. The exception is by ownership, not by flag: a compile target of the shape (the `.d.ts` map) is rewritten every time. `model.json` is the author's file and is not edited - the result carries the fragment to merge.
- **Area choice is free for the model; output shape is not.** Measured: it never picks the wrong area for a file it just edited. So no token-saving facades (dispatch by extension, one universal `validate`); that budget goes to the result JSON — one shape per command, findings distinguishable from "the tool could not run", no shape built before the feature needing it.

## `meta validate`: one endpoint, no switches, all checks listed

`a2 meta validate <endpoint>` reports what the metadata layer refuses about one endpoint. What it checks and why those stages: [Platform/A2v10.Metadata/CLAUDE.md](../../Platform/A2v10.Metadata/CLAUDE.md), "Validation". Decided and built 2026-09-20.

- **The leaf is `validate` because `view validate` already exists.** The cross-cutting verb repeats per area and the AREA says what is validated — spelling it `check-metadata` to be explicit would break the one rule that keeps placement derivable. The word "metadata.json" belongs in `--help`, not in the name, and would be wrong there anyway: the unit is the endpoint, which is four files.
- **The argument is one endpoint and is required; an application-wide form is never built.** Observed, not feared: a model handed a sweep runs it on every edit. Omitting the argument is a refusal, not "all". This does not prevent a loop over endpoints — nothing can — it removes the cheap way and lets `--help` say the rule in words: validate the endpoint you just edited.
- **One option, and it arrives with the stage it turns on.** The three file stages are free and chained, so a flag that switches one off buys only a way to get it wrong. `--database` adds a stage on top; `--all` is not built, because today it would be a synonym for the default and the model would type it at random.
- **The result lists EVERY check that exists FOR THIS ENDPOINT, with a status** — `{ "endpoint", "checks", "error" }`. "Not checked" has to read on the spot, or a missing key is read as a passing one. Which stages exist follows the endpoint's type and is known only after the load, so the map of a report is two keys and of a system endpoint one — a short map is the answer, not a truncated one. Statuses are three (`passed`, `failed`, `unknown`); `skipped` means "not requested" and cannot happen until `--database` exists, so it arrives with it. `unknown` is the one that must not be mistaken for consent: requested, not reached, nothing known. `blocked` and `waiting` were refused — the second promises a later run that never comes.
- **`error` is a string, and it never travels through `JsonResult.Try`.** Which check failed is the single `failed` in the map, so a field repeating it is a second place to disagree. The `Try` shape means the tool could not run: a validator letting its own stage's exception escape into it destroys the one distinction the result exists to make.

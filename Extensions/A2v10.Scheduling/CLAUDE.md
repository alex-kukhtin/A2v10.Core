# A2v10.Scheduling

The command queue (`a2sch.Commands`, polled by `ProcessCommandsJobHandler`) and the Quartz jobs around it.

## The queue is FIFO, so "after a batch" is just a later command

There is no batch, no group, no continuation entity. A command that must run after a set of commands is queued after them; order in the queue *is* the dependency. "Recalculate totals after 100 postings" is one more command, queued last.

This is a decision about what the queue guarantees, and it rests on three things:

- **`Command.List` takes the lowest Ids.** `select top(@Limit) ... order by Id` inside a CTE, then `update` over the CTE. `update top(@Limit)` on its own picks arbitrary rows — T-SQL does not allow `order by` on `update`, and `top` without it is unordered. The `order by b.Id` on the final `select` only orders rows already captured; it never decided which ones.
- **One tick of a job at a time.** `GenericJob` is `[DisallowConcurrentExecution]`, so a later command cannot start while an earlier one is still running. Quartz keys the attribute by JobDetail (JobKey), not by class, so the jobs from configuration still run in parallel with each other — they share `GenericJob` but not a key. Misfire policy stays default: a cron fire blocked by a running tick runs once immediately afterwards, which for a queue poller is the wanted behaviour, not a missed run to suppress.
- **One application instance.** Quartz runs on RAMJobStore, non-clustered, so each instance has its own scheduler and its own idea of what is running. Two instances on one database poll the queue at the same time and FIFO across them does not hold. Restoring it there means refusing to take a command while any locked incomplete command with a lower Id exists — a strictly serial queue, with the throughput that implies. Not built. Revisit only when the application is actually deployed multi-instance.

Stale locks are pre-existing and untouched: a process killed mid-command leaves `Lock is not null, Complete = 0` forever, and nothing ever reclaims it. Ordering is unaffected — `Command.List` skips locked rows rather than waiting — so a trailing command will run as if the dead one had finished.

## Ordering inside one Collection.Queue call — not built (2026-09)

`a2sch.[Command.TableType]` has no ordering column, and it cannot be faked: TVP rows have no order, and `insert ... select` fires the `Id` default constraint (`next value for a2sch.SQ_Commands`) per row in an undefined order. So a single `QueueCommandsAsync` call cannot say "this one goes last". Queue the trailing command with a separate `Command.Queue` call after the list, or queue the items one by one — insert time then orders the Ids.

The cost accepted: a crash between the two calls leaves the trailing command unqueued, and whatever it recomputes stays stale until something queues it again.

If it ever becomes necessary, the shape is known: `[Order] int` in the table type (bracketed — reserved word), `Id` listed explicitly in the insert and taken from `next value for a2sch.SQ_Commands over (order by [Order])`; the `over` form is the only one that numbers rows in a chosen order, and it allows no `PARTITION BY`. `ScheduledCommand` gains an `Order` property. Rejected for now because that property is public surface of the package — every caller has to decide about a field that is 0 almost always.

## Rejected on the way here

- **A batch with a continuation** (child rows pointing at the command that waits for them; `Command.List` skips a row while incomplete children reference it). Correct and durable, but it invents an entity for what ordering already expresses.
- **Coalescing in `Command.Queue`** (skip the insert when an unlocked identical command is pending, so a burst collapses into one run). That is a throttle — "at most once per N seconds" — not "after". It answered a different question than the one asked.

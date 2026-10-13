# platformid — the type of a key, declared by the application

> What it is, why it is declared this way, what was refused. Decided 2026-10-13. Not built yet: see State.

## What

- **`dbo.platformid` is a SQL Server alias type: the type of the application's record keys** — `bigint`, `int` or `uniqueidentifier`. One per database, for its whole life: a column on an alias type cannot be altered, so changing it is done by hand (recreate everything that uses it).
- **The application declares it in `_sql/platformid.sql`** (the main module), listed FIRST in the host's `sql.json` that builds `full.sql` — before the platform scripts:
  ```sql
  if type_id(N'dbo.platformid') is null
  	create type dbo.platformid from bigint;
  go
  ```
- **The platform's own scripts may use it.** First user: `a2blob.BlobLinks.LinkId` — a link to an application record of any table ([blobs.md](blobs.md)).

## Why

- **The first platform table that points at application rows.** `BlobLinks.LinkId` holds the owner's key, and owner keys are `platformid`: `bigint` in one application, `uniqueidentifier` in another. A fixed `bigint` breaks the second.
- **There was a cycle.** `full.sql` = the platform scripts → `a2v10_metadata.sql` → `deploydatabase.sql` → `app.sql`. `dbo.platformid` was created inside `deploydatabase.sql` (`SqlDbGenerator.CreatePlatformIdScript`, from `app.json`) — after the platform scripts, which therefore could not use it; and `deploydatabase.sql` needs the platform before it. The type depends on nothing but one fact of the application, so it goes first, before anything — it sat in the generated deploy only because that was who happened to generate it.
- **In `_sql/`, not `@sql/`.** `@sql` is the asset package's: `CopyAssetsToProject` copies the platform scripts there on every build, as it fills `@schemas` and `@types` — build output, not edited by hand, a file of the package's with the same name writes over the author's. `_sql/` is where the scaffold keeps the SQL the author writes (`_schemas.sql`); the main module's `sql.json` does not pick `platformid.sql` up, so it runs once, first in `full.sql`, and never in `app.sql`.
- **Written by the author, not generated.** Classic applications need the type as much (attachments) and do not run the metadata deploy. One file, the same in both worlds.

## How it fails, and how it teaches

- **Listed in `sql.json` and absent:** `full.sql` is not built — the explicit input fails to read (`SqlJsonBuilder`, as its MSBuild original).
- **Not listed:** the platform script stops at its own `throw` — the first statement of `a2v10_struct_simple.sql`, the first script of the bundle, so before anything is created and not only before `BlobLinks`: the type is a precondition of the platform, and more platform tables will stand on it. The message carries the fix — which file, where in `sql.json`, its text, the allowed types, "the same as `platformid` in `app.json`". The error is the documentation at the moment it is needed, and lives beside the code that needs the type, so it cannot go stale apart from it. Without the check SQL Server says "Cannot find data type platformid", which names no fix.
- **Written twice in a metadata application** — `"platformid"` in `app.json` and this file. A disagreement is refused by the deploy, which compares `app.json` with the database already.

## Refused

- **`LinkId nvarchar` or `sql_variant`** — every type at once, but the join to the owner's key converts and the index on one side is lost.
- **The platform script creating the type with `bigint` by default** — a `uniqueidentifier` application then never deploys: its type is taken before it can declare one.
- **A platform procedure (`BlobLinks.Ensure`) with dynamic DDL**, called by the metadata deploy and by a classic `schema.sql` — a platform table whose existence depends on who remembered to call what.
- **Skipping `BlobLinks` when the type is missing** — a silent state: deploy passes, the first attachment fails far from the cause.
- **Generating the file from `app.json`** — only metadata applications run the CLI deploy; classic ones would still write it by hand, so there would be two ways to get one file.

## Why `CreatePlatformIdScript` was removed

The metadata generator wrote `if type_id(N'dbo.platformid') is null create type dbo.platformid from <app.json>` into `deploydatabase.sql`. Kept beside the author's file, it would be:

- **Dead on the road that matters.** `full.sql` runs the author's file first and the platform's check second, so by `deploydatabase.sql` the type always exists and the `if` never holds.
- **A mask on the road that does not.** A plain deploy (`a2 meta deploy`, no `--full`) runs `deploydatabase.sql` without the platform scripts. A metadata application that forgot `_sql/platformid.sql` would get its type there, silently, from `app.json` — and fail later, on the first `full.sql` of another environment, far from the cause. A classic application has no such road and fails at once. One rule would be learned by half the applications.
- **A second creator of one fact.** The type would have two sources — the file and `app.json` through the generator — and which one created it would depend on which script ran first on that database.

Removed, the type has one creator, the author's file, and one failure, the platform's check, the same for every application.

## Consequences

- **Every database needs the type now, classic included.** An existing one gets the teaching error once, at its next update of the platform scripts.
- **`CreatePlatformIdScript` is gone from the metadata generator** — the type always exists before `deploydatabase.sql`, so it never fired; removed, so the type is declared in one place. A plain deploy (not `--full`) assumes the database was made by `full.sql` once, as it already assumes the platform.
- **Skill:** the scaffold carries `_sql/platformid.sql` (`bigint`) and its line first in the host `sql.json` (done here, on the owner's word — files, not text). The blind instance's to write: the rule "the type of the application's keys is declared once, in `_sql/platformid.sql`, and is never changed"; a metadata application keeps it equal to `app.json` `platformid`.

## State

Built 2026-10-13: the check (`a2v10_struct_simple.sql`), `LinkId platformid` (`a2v10_blobs_simple.sql`), `CreatePlatformIdScript` removed; on MetaAppStand `MainApp/_sql/platformid.sql` (`bigint`, as its `app.json`) first in `WebApp/sql.json` — the one host in reach that builds `full.sql`; the skill's scaffold: `MainApp/_sql/platformid.sql` (`bigint`) first in `WebApp/sql.json`. Not yet: the skill's text — the rule where a model reads it (the blind instance's).

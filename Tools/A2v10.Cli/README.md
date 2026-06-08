# A2v10.CLI

Command-line tool for the [A2v10](https://a2v10.com) platform. It inspects an A2v10
project — its configuration, database, and endpoints — and returns everything as
**JSON**.

Designed to be driven by an LLM coding assistant (Claude Code, Copilot, etc.): the
output is machine-readable by default, every call is self-contained, and there are no
interactive prompts. A human can run it just as well.

## Install

It is a .NET global tool (requires the .NET SDK):

```
dotnet tool install --global A2v10.CLI
```

Update an existing install:

```
dotnet tool update --global A2v10.CLI
```

Verify:

```
a2 --version
```

## Working directory

Run `a2` from the **application root** — the project root, **not** `WebApp`, `MainApp`,
or any module folder. The tool resolves configuration, modules, and the database
connection relative to the current directory; from the wrong folder it will not work.

## Configuration

The tool reads the connection string from `appsettings.json` in the current directory:

```json
{
  "ConnectionStrings": {
    "Default": "Server=.;Database=mydb;Trusted_Connection=True;"
  }
}
```

User Secrets (`secrets.json`, the same store Visual Studio manages) are also honored and
override `appsettings.json` — handy for keeping the connection string out of the project.

## Output format

Every command returns JSON.

Success:
```json
{ "success": true, "data": { ... } }
```

Error:
```json
{ "success": false, "error": { "message": "..." } }
```

## Table identifier format

`schema.[Table]` — used both as command arguments and in responses.

Examples: `cat.[Agents]`, `doc.[Invoice]`, `cat.[Agent.Addresses]`.

Brackets are required — table names may contain dots.

## Commands

### `a2 app config`

Returns application-level configuration: multi-tenancy flag and the list of modules.

```
a2 app config
```

```json
{
  "success": true,
  "data": {
    "multiTenant": false,
    "modules": [
      { "prefix": "$admin",    "root": null },
      { "prefix": "",          "root": "MainApp" },
      { "prefix": "$meta",     "root": null },
      { "prefix": "$workflow", "root": null }
    ]
  },
  "error": null
}
```

- `multiTenant` — whether the project is multi-tenant.
- `modules` — application modules. Each has a `prefix` (the literal URL token, including
  `$`; `""` is the main app) and a `root` (the module's source folder from the project
  root, or `null` when the module has no local folder).

### `a2 db tables [schema]`

Lists database tables, grouped by schema. Optional `schema` filters by schema name.

```
a2 db tables
a2 db tables cat
```

```json
{
  "success": true,
  "data": ["cat.[Agents]", "cat.[Units]", "cat.[Currencies]", "doc.[Invoice]"]
}
```

### `a2 db table-columns <table>`

Returns the structure of a table. Conventions: `Id` is always the primary key, FK
columns are nullable.

```
a2 db table-columns cat.[Agents]
```

```json
{
  "success": true,
  "data": {
    "columns": [
      { "name": "Id",     "type": "bigint" },
      { "name": "Name",   "type": "nvarchar(255)" },
      { "name": "Code",   "type": "nvarchar(16)" },
      { "name": "Agent",  "type": "bigint", "ref": "cat.[Agents]" },
      { "name": "Parent", "type": "bigint", "ref": "cat.[Agents]", "identity": true }
    ]
  }
}
```

- `ref` — reference to another table, in canonical format.
- `identity: true` — auto-increment column.

### `a2 db referenced-by <table>`

Lists tables and columns that reference the given table.

```
a2 db referenced-by cat.[Agents]
```

```json
{
  "success": true,
  "data": [
    { "table": "doc.[Invoice]",  "column": "Agent" },
    { "table": "doc.[Order]",    "column": "Agent" },
    { "table": "cat.[Contacts]", "column": "PrimaryAgent" }
  ]
}
```

### `a2 endpoint resolve-* <route>`

Resolves a single model.json element the way the runtime sees it: the view/template
files it is bound to, the SQL procedures it calls, and the data-model type tree it
returns. Useful for verifying that the layers (model.json, XAML, SQL) agree after a
deploy.

`route` addresses the element as `/[$<module>]/<path>/<element>`, e.g.
`catalog/agent/edit`. No record `id` is needed — types come from the result-set schema.

One command per model.json section:

| Command | Section | Kind |
| --- | --- | --- |
| `a2 endpoint resolve-action <route>` | `actions` | page |
| `a2 endpoint resolve-dialog <route>` | `dialogs` | modal |
| `a2 endpoint resolve-popup <route>`  | `popups`  | popup |
| `a2 endpoint resolve-command <route>` | `commands` | callable |

The three renderable commands share one output shape:

```
a2 endpoint resolve-action catalog/agent/edit
```

```json
{
  "success": true,
  "data": {
    "route": "catalog/agent/edit",
    "model": "Agent",
    "view":     { "dir": "catalog/agent/edit", "file": "view.dialog.xaml" },
    "template": { "dir": "catalog/agent/edit", "file": "edit.template.ts" },
    "sqlProcedures": {
      "load":   "cat.[Agent.Load]",
      "update": "cat.[Agent.Update]"
    },
    "dataModel": {
      "types": {
        "TRoot":  { "props": { "Agent": { "type": "TAgent", "len": null } }, "id": null, "name": null },
        "TAgent": { "props": { "Id":   { "type": "number", "len": null },
                               "Name": { "type": "string", "len": 100 } }, "id": "Id", "name": "Name" }
      }
    }
  },
  "error": null
}
```

`resolve-command` is similar but for callables: `view`/`template` are absent,
`sqlProcedures` is what the command actually calls, and `dataModel` is present only if
the command returns a model.

These commands require the project to be deployed (migrations applied, build done). Run
before deployment, they fail by design.

## License

See the package license.

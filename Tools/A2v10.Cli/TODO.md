# A2v10.Cli — TODO

## `a2 report validate <file>`

Same as `view validate`, for a report template: load it the way the engine does, return findings.

- **Area `report`** — the platform's word (`model.json` key, `IReportEngine`, `ReportTemplateFile`). A print blank is the same file under the same reader, so one role.
- **Body:** `ReportTemplateFile.Open` → `TemplateReader.ReadReport`. Catches malformed markup, unknown elements/properties, bad JSON, empty file, a root that is not a `Page`.
- **Extension:** none given → probe (`ReportTemplateFile.Extensions`), as the engine does. A known one given → that exact file: with `f.vxaml` and `f.json` side by side, probing would silently check the other one.
- **Price:** `ProjectReference` on `ReportEngines/A2v10.Xaml.Report`.
- **Out of scope:** the `Model` section — `ReadReport` does not read it; its grammar and shape check are print-form questions.

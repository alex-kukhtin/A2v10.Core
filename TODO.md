# A2v10.Core — TODO

## Xaml: grouping (`GroupDescription`, `DataGrid.GroupBy`, `CollectionView.GroupBy`)

Help documents `DataGrid.GroupBy` as a comma-separated string only, until these are settled.

- **`GroupDescription.Collapsed` is dead.** Declared, never emitted by `GetJsValue`. Client reads `source.expanded === false` (`main.js`, data-grid `$groups`). Fix: `if (Collapsed) yield return "expanded: false";`.
- **Two separators for one list.** Xaml string → `GroupDescriptionsConverter` splits on `,`. Bound string → client splits on `-` (`$groupCount`, `$groups`). Same `"A,B"` through `Bind` = one group on field `"A,B"`; a field name with `-` breaks bound grouping. Pick one separator.
- **`CollectionView.GroupBy` is dead.** Renders `:initial-group`; only `collection-view-server-url` declares `initialGroup`, and never reads it (its `GroupBy` comes from `$ModelInfo.GroupBy`). `collection-view`, `collection-view-server` have no such prop. Remove, or wire it.
- **Open question: server grouping.** `collection-view-server-url` exposes `Parent.Grouping.GroupBy` and sends `group` in the query → `ModelInfo.GroupBy`. Is `DataGrid GroupBy="{Bind Parent.Grouping.GroupBy}"` the intended pairing (would explain the `-` separator)? If yes — what value does the server return in `GroupBy`.
- `GroupDescription.Title` placeholders `{Value}`, `{Count}`: `String.replace` with a string — first occurrence only.

## Client (`main.js`)

- **`utils.text.maxChars` truncates text that fits.** `if (text.length < length)` → text of exactly `length` chars loses its last char to `…`. Fix: `<=`. Seen via `PropertyGrid(Item).NameMaxChars`/`ValueMaxChars` (`$maxChars`).
- **Check: `$requeryNew` in a dialog.** Commits `setnewid` unconditionally → `history.replaceState` rewrites the page URL under the dialog. `$save` guards the same commit with `if (!self.inDialog)`.

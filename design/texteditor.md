# Text editor — a comment with bold and images: `a2-text-editor`, `a2-text-view`, hooks

> Decisions, why, and what was refused. Names marked *working* may change. Decided 2026-10-14; behaviour carried from the TaskTracker `CommentEditor` (Vue 3, Tiptap) design of 2026-10-08, without mentions.
>
> Built 2026-10-14: `Scripts/texteditor.js` + `themes/tabbed/TextEditor.less`, XAML `TextEditor`/`TextView`, `$uploadBlobFile`, `std:url.blobUrl`. Live sample: `task/` on MetaAppStand (create, edit, comments with inline edit). Playground (no server, open as a file): `Web/A2v10.Core.Web.Site/TestPages/texteditor.html`, fake hooks.

## Where

- **`Web/A2v10.Core.Web.Site/Scripts/texteditor.js`**, one IIFE, two components: `a2-text-editor` (edit) and `a2-text-view` (show). In `inputFiles` of both bundles, `tabbed.js` and `tabbedsp.js`.
- **XAML wrappers** `TextEditor`, `TextView` (`ViewEngines/A2v10.ViewEngine.Xaml/Controls`) — `UIElementBase`, as `BlobImage`/`BlobPreview`: `Label`, `Required` and the rest of `Control` are not needed. `Delegates` → `:delegates` (a binding to an object of functions in the model), as `Calendar`.
  - `TextEditor`: `Value` → `:item` + `prop` (writes back), `Placeholder`, `Disabled` (bindable), `Delegates`. A read-only model (or `Render="ReadOnly"`) disables it as it disables every `Control` — one rule, `UIElementBase.IsReadOnlyByModel`; otherwise a posted record would take typing and image uploads (an attachment written to it).
  - `TextView`: `Value` → `:value` (shown, not written — as `BlobPreview`), `Delegates` (`ImageUrl` only is asked).
- **The render is a local function of the IIFE**, shared by both components: the same function shows the text and loads it into the editor. Refused: a module `app.modules['std:…']` — nothing outside the file calls it; showing is a component, not a call from a template.

## Own `contenteditable`, no dependencies, no build

- Refused: Tiptap (no Vue 2 wrapper, no build without a bundler; ~120 KB gzip), bare ProseMirror (~63 KB gzip — more than `vue.min.js`, 41), Quill (57). Squire (18) — only if lists, quotes or markup of pasted HTML are ever wanted.

## Format: markdown of three elements

- `\n`, `**bold**`, `![name](src)`. No paragraphs: `\n` is enough — the tracker has none either; an empty line is two `\n`. A link is text; the view makes it clickable. A user's `\` and `*` are escaped with `\`.
- **`src` is the template's**, whatever `UploadImage` returned. The component never interprets it — it only asks `ImageUrl`.

## Render: markdown → HTML

- Escape everything; `\n` → `<br>`, `**` → `<b>`; `![](src)` → `<img>` only if `ImageUrl(src)` returns a URL, otherwise text (a spy pixel); `http(s)` links → `<a target=_blank rel=noopener>` — in the view only. The output is only its own markup: `v-html` is safe.
- In the editor `<img>` keeps the original in `data-src`; `src` is what `ImageUrl` returned.
- **In the view an image is a link to its original** (`target=_blank`) — `max-height` cuts it — and `loading=lazy`.
- **Until loaded — `img.loading`**: a fixed block (the size is unknown; a lazy image needs a box to start loading) with a spinner as an animated SVG in the background; removed on `load`/`error` caught on the root's capture (they do not bubble). An error shows a broken image, not an endless spinner. On the `img` itself, in the view and in the editor alike: the tracker's wrapper existed for `::before`/`::after` only, and a wrapper in `contenteditable` breaks editing. Refused: the text "Завантажується…" — no such key in the platform's locale, and a language is not hard-coded in platform CSS.

## Hooks: `delegates`

- The component calls a hook as a template's function is called: `this` = the model of the page (`$root.$data`, as `emptyPanelCallback`), so a hook reaches `this.Attachments`, `this.$ctrl` with no captured root. A missing hook — the feature is off.
- **The hooks object is built once, in the template text, and passed by reference**: a template property returning a new object on every read is a new prop of every `TextView` on every render of the page, and each one parses its text again. The hooks are a constant of the template text, the property returns it; the model comes as `this`.
- **`UploadImage(file) → Promise<string | null>`** — the string goes into `![name](…)`; `null` → the `img` is removed (the toast is the template's). Before the promise resolves, the address must already be visible to `ImageUrl`: the editor calls it right after the answer.
- **`ImageUrl(src) → string | null`** — pure; called inside the view's computed, so a model it reads (`Attachments`) re-renders the view by itself. No hook → no images, all text.
- **`Submit()`** — Ctrl/⌘+Enter. No arguments.
- **Why `ImageUrl` and not a fixed "platform address"**: the `_blob` token is hash(`AccessToken`, user) ([blobs.md](blobs.md), "Access") — a URL with a token stored in the text opens for the uploader only. The template stores its own form (`_blob/<id>`, no token) and gives each reader a URL with the reader's token. The filter is the template's too: an address it does not know stays text.
- **No "uploading" state in the component.** The promise of `UploadImage` is the template's, so the template knows an upload is running and closes its own button and `Submit`. In exchange: an image with `blob:` is never written to `item[prop]` — the model never holds a dead address, whatever saves it. Refused: `$emit`/a hook for "uploading" — the template already knows.

## Images: attachments of the owner

- **An image is an attachment of the text's owner** (`BlobLinks`, `base` + owner id) from the moment it is pasted, not from the moment the text is saved. The text holds `_blob/<id>` and nothing else.
- **`Load` issues tokens by the owner's links**, never by ids found in the text: a `_blob/<foreign id>` written by hand gets no token and stays text. A model that shows texts carries the attachments of their owners with tokens — on a page of one owner a `select` over its links, in a feed of many a join by the owners shown. Nothing for the platform here.
- **An image removed from the text stays an attachment** — visible in the owner's files and deleted as any of them; once its link is deleted, nothing references the blob, and the collector finds it by FK ([blobs.md](blobs.md), "No garbage collection"). No orphans, no cleanup of their own.
- **A deleted attachment still referenced by a text** shows as text `![name](_blob/<id>)`: `ImageUrl` finds nothing. Nothing breaks.

## Platform inputs the hooks are written with

- **`$uploadBlobFile(url, file, data, opts)`** — `UploadImage`: `paste`/`drop` already have the file, `$uploadBlob` always opens the picker. Returns the attachment row `{ Id, Blob: { Id, Token, Name, Mime } }`, or `false` after an alert (the toast of the hook). No `closeAllPopups`: the editor may itself live in a popup. Refused: `opts.file` (opts are behaviour, not the payload), `File` as `accept` of `$uploadBlob` (one argument, two meanings).
- **`require('std:url').blobUrl(blob)`** — `ImageUrl`: a model element (`$id`, the token by `_meta_.$token`); the template appends the uploaded row to its model before the promise resolves, so it always has one. A module, not `$ctrl`: `ImageUrl` runs inside render, the first mount included, before the page has its `$ctrl` — a hook called from render reaches nothing of the controller.
- **`$ensureSaved()`** — a new owner has no id, and an attachment needs one: the main element (`$main`, else the first property of the model — the rule `$save` resolves by) is saved if `$isNew`, marked dirty first (a new element with nothing typed is clean, and `$save` skips a clean model). Concurrent calls share one save — two images pasted at once would create two elements. `true` when the element has an id after it. The root merge replaces an object property, so the element is read again after the save. Refused: an alert "save first" in the template.
- **`options.skipDirty` takes an array path** — `'Comments'` covers its `$append`/`$remove`/`$empty`, as `'Comments[].Text'` covers a property of its elements: the array calls `$setDirty(true, path)`, the check is on `prop ? path.prop : path`. The `query` guard stays for properties only (`path && prop && …`): an array named `QueryLines` would otherwise stop marking the page dirty. What a command or an upload has already stored (a comment, its edit, an attachment) is no change for Save of the page; client state of the root is `$$`. Refused: restoring the flag by hand around each write, and `$ctrl.$nodirty` — its restore is deferred and wipes the description's own change written right after an upload.
- `$uploadBlobFile` in [blobs.md](blobs.md), "Upload and read".

```ts
async UploadImage(file: File) {
	if (!await this.$ctrl.$ensureSaved()) return null;
	const row = await this.$ctrl.$uploadBlobFile('attachment/Upload', file, { Id: this.Task.Id, Base: '/task' });
	if (!row) return null;
	this.Attachments.$append(row);   // options: { skipDirty: ['Attachments'] }
	return `_blob/${row.Blob.Id}`;
},
ImageUrl(src: string) {
	const m = /^_blob\/(\d+)$/.exec(src);
	const a = m && this.Attachments.find(x => x.Blob.Id == +m[1]);
	return a ? urltools.blobUrl(a.Blob) : null;   // const urltools = require('std:url')
}
```

## Editor behaviour

- **Props:** `item`, `prop`, `placeholder`, `disabled`, `delegates`.
- **Sync:** `input` → DOM to markdown → `item[prop]`, the value remembered; `watch` on `item[prop]` different from the remembered one (cleared after sending, a record loaded) → `innerHTML` = render. Own input is never re-rendered — the caret stays.
- **DOM → markdown:** text escaped, `&nbsp;` → space; `b`/`strong` → `**…**`, empty — skipped; `br` → `\n`, except the last `br` of a block (Chrome's empty-line stub); `div`/`p` — a new line unless at the start (Enter: Chrome — `div`, Firefox — `br`); `img` → `![alt](data-src)`, `blob:` skipped; anything else (`span style`) — transparent, children only; trailing `\n` trimmed.
- **Keys:** Ctrl+B — the browser's, `styleWithCSS = false` on focus (`<b>`, not `<span style>`); Ctrl+I/U blocked (lost on save); Ctrl/⌘+Enter — `Submit`.
- **`paste`:** images → upload; otherwise `preventDefault` + `insertText` of plain text — foreign HTML never enters. **`drop`:** image files — at the drop point (`caretPositionFromPoint` / `caretRangeFromPoint`); text from outside — plain text; a move inside the editor (marked by `dragstart`) — the browser's.
- **Images:** png/jpeg/gif/webp; `image.png` → `Знімок yyyy-MM-dd HH-mm-ss.png` (a screenshot is always `image.png`). `blob:` + `execCommand('insertHTML', '<img … class=uploading>')` — through `execCommand` for the native undo; `uploading` — semi-transparent (no skeleton or "uploading…": a wrapper around `img` in `contenteditable` breaks editing). On the answer: find the `img` with this `blob:` in `$el` (erased — nothing); success — `data-src` = the returned string, `src` = `ImageUrl` of it, class removed; `null` — remove the `img`; then `revokeObjectURL` and write `item[prop]` again (an attribute change fires no `input`).
- **Links are highlighted, not wrapped** — CSS Custom Highlight API (`CSS.highlights`, `::highlight(a2-link)`): ranges over text nodes on every render and `input`, found by the same function as the view's links. The DOM, the caret, undo and DOM → markdown are untouched; a loaded link and a typed one go one way. The registry is global: one `Highlight` shared by all editors, each holds its own ranges. A plain click edits (places the caret, selects an image); **Ctrl/⌘+click opens** an image (its `src`, the reader's URL; one still uploading has none) or a link — the same ranges the highlight paints (`linkRanges`), hit by their client rects, not by the caret (a caret lands on the nearest boundary, a click past the end of a line would open the link). ⌘ for Mac, where Ctrl+click is the context menu. The tab opens in the background: the browser takes the modifier from the gesture, as for Ctrl+click on any link — accepted; a double click without a modifier would open it in front, but it selects a word or an image. A disabled editor (a read-only model) opens on any click — it cannot be edited, and the description reads as the view. No hint text: no such key in the platform's locale. A link cut by bold into two text nodes is not highlighted whole — accepted. No API (Firefox < 140) — no highlight. Refused: `<a>`/`<span>` in the editor — re-rendering during input moves the caret.
- **Placeholder:** CSS `:empty::before { content: attr(data-placeholder) }`; an empty result on `input` → `innerHTML = ''` (Chrome leaves a `<br>`).
- **Not handled:** redo during an upload (brings `blob:` back), adjacent bolds merged (`**а****б**`), Chrome's `span style` on delete (bold inside it lost).

## Open

- **Working names:** `a2-text-editor`, `a2-text-view`, `TextEditor`, `TextView`, `UploadImage`, `ImageUrl`, `Submit`.

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

// A comment: own contenteditable, the text is markdown of three elements — \n, **bold**, ![name](src).
// Decisions — design/texteditor.md.

(function () {

	const IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'];

	function escapeHtml(s) {
		return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
	}

	function escapeMd(s) {
		return s.replace(/[\\*]/g, '\\$&');
	}

	// One rule for a link: the view makes it clickable, the editor highlights it. Trailing punctuation belongs to the sentence
	function* findLinks(s) {
		for (let m of s.matchAll(/https?:\/\/[^\s<>"]+/g))
			yield { index: m.index, url: m[0].replace(/[.,;:!?)]+$/, '') };
	}

	function textHtml(s, view) {
		if (!view)
			return escapeHtml(s);
		let out = '';
		let last = 0;
		for (let { index, url } of findLinks(s)) {
			out += escapeHtml(s.substring(last, index));
			out += `<a href="${escapeHtml(url)}" target="_blank" rel="noopener">${escapeHtml(url)}</a>`;
			last = index + url.length;
		}
		return out + escapeHtml(s.substring(last));
	}

	// The links of the editor's text, as ranges over its text nodes: the highlight paints them, a click opens them — one rule.
	// A link cut by bold into two text nodes is not a link here — accepted
	function linkRanges(el) {
		let ranges = [];
		let walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
		for (let n = walker.nextNode(); n; n = walker.nextNode()) {
			for (let { index, url } of findLinks(n.nodeValue)) {
				let r = new Range();
				r.setStart(n, index);
				r.setEnd(n, index + url.length);
				ranges.push(r);
			}
		}
		return ranges;
	}

	// by the rects, not by the caret: a caret lands on the nearest boundary, a click past the end of a line would open the link
	function hit(range, x, y) {
		return Array.from(range.getClientRects()).some(c => x >= c.left && x <= c.right && y >= c.top && y <= c.bottom);
	}

	// The CSS Custom Highlight API paints over the text: the DOM, the caret and undo stay as they are.
	// The registry is global, so editors share one Highlight, each holding its own ranges. No API — no highlight
	const HIGHLIGHT = 'a2-link';

	function linkHighlight() {
		if (!window.CSS || !CSS.highlights) return null;
		let hl = CSS.highlights.get(HIGHLIGHT);
		if (!hl) {
			hl = new Highlight();
			CSS.highlights.set(HIGHLIGHT, hl);
		}
		return hl;
	}

	const IMAGE_RX = /^!\[((?:\\.|[^\]\\])*)\]\(([^)\s]+)\)/;

	// The one render: the view and the text loaded into the editor. Everything is escaped; the output is own markup only.
	// An image is an <img> only if imageUrl gives it a URL — otherwise text (a spy pixel); data-src keeps the stored form.
	// view: links clickable, an image is a link to its original (max-height cuts it) and lazy.
	// .loading — a fixed block with a spinner in the background until load/error (from Azure it takes seconds); on the img itself,
	// no wrapper: a wrapper in contenteditable breaks editing, and the editor loads images too
	function toHtml(md, imageUrl, view) {
		let out = '';
		let text = '';
		let bold = false;
		const flush = () => { out += textHtml(text, view); text = ''; };
		for (let i = 0; i < md.length;) {
			let ch = md[i];
			if (ch === '\\' && i + 1 < md.length) {
				text += md[i + 1];
				i += 2;
			} else if (ch === '*' && md[i + 1] === '*' && (bold || hasClose(md, i + 2))) {
				flush();
				out += bold ? '</b>' : '<b>';
				bold = !bold;
				i += 2;
			} else if (ch === '\n') {
				flush();
				out += '<br>';
				i++;
			} else if (ch === '!' && md[i + 1] === '[') {
				let m = IMAGE_RX.exec(md.substring(i));
				let url = m ? imageUrl(m[2]) : null;
				if (url) {
					flush();
					let name = m[1].replace(/\\(.)/g, '$1');
					let href = escapeHtml(url);
					let img = `<img src="${href}" data-src="${escapeHtml(m[2])}" alt="${escapeHtml(name)}" class="loading"`;
					out += view ? `<a href="${href}" target="_blank" rel="noopener">${img} loading="lazy"></a>` : `${img}>`;
					i += m[0].length;
				} else {
					text += ch;
					i++;
				}
			} else {
				text += ch;
				i++;
			}
		}
		flush();
		return out;
	}

	// A hook is called as a template's function is: this = the model of the page (as emptyPanelCallback). No hook — undefined
	function hook(vm, name, ...args) {
		let d = vm.delegates;
		return d && d[name] ? d[name].apply(vm.$root.$data, args) : undefined;
	}

	// load/error do not bubble — the root sees them on capture. An error too: a broken image, not an endless spinner
	function loaded(ev) {
		if (ev.target.tagName === 'IMG')
			ev.target.classList.remove('loading');
	}

	// an unpaired ** is text
	function hasClose(md, i) {
		for (; i < md.length; i++) {
			if (md[i] === '\\')
				i++;
			else if (md[i] === '*' && md[i + 1] === '*')
				return true;
		}
		return false;
	}

	// Enter: Chrome makes a div, Firefox a br. The last br of a block is Chrome's stub of an empty line.
	// An img without data-src (still uploading — blob:) is not written: the model never holds a dead address.
	function toMarkdown(root) {
		let out = '';
		let breakAfterBlock = false;
		const emit = (s) => {
			if (breakAfterBlock) {
				out += '\n';
				breakAfterBlock = false;
			}
			out += s;
		};
		const walk = (node, inBold) => {
			for (let n = node.firstChild; n; n = n.nextSibling) {
				if (n.nodeType === Node.TEXT_NODE) {
					if (n.nodeValue)
						emit(escapeMd(n.nodeValue.replace(/\u00a0/g, ' ')));
					continue;
				}
				if (n.nodeType !== Node.ELEMENT_NODE)
					continue;
				switch (n.tagName) {
					case 'BR':
						if (!n.nextSibling && (node.tagName === 'DIV' || node.tagName === 'P'))
							break;
						emit('\n');
						break;
					case 'B':
					case 'STRONG': {
						if (inBold) {
							walk(n, true);
							break;
						}
						emit('');
						let start = out.length;
						out += '**';
						walk(n, true);
						// an empty bold is skipped
						if (out.length === start + 2)
							out = out.substring(0, start);
						else
							out += '**';
						break;
					}
					case 'DIV':
					case 'P':
						if (out)
							out += '\n';
						breakAfterBlock = false;
						walk(n, inBold);
						breakAfterBlock = true;
						break;
					case 'IMG': {
						let src = n.getAttribute('data-src');
						if (src)
							emit(`![${(n.alt || '').replace(/[\\\]]/g, '\\$&')}](${src.replace(/[\s)]/g, encodeURIComponent)})`);
						break;
					}
					default:
						walk(n, inBold);
				}
			}
		};
		walk(root, false);
		return out.replace(/\n+$/, '');
	}

	function images(files) {
		return Array.from(files || []).filter(f => IMAGE_TYPES.includes(f.type));
	}

	// a screenshot from the clipboard is always image.png
	function named(file) {
		if (!/^image\.\w+$/i.test(file.name))
			return file;
		let d = new Date();
		let p = (n) => String(n).padStart(2, '0');
		let stamp = `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}-${p(d.getMinutes())}-${p(d.getSeconds())}`;
		return new File([file], `Знімок ${stamp}.${file.name.split('.').pop()}`, { type: file.type });
	}

	function caretAt(x, y) {
		let r = null;
		if (document.caretPositionFromPoint) {
			let p = document.caretPositionFromPoint(x, y);
			if (p) {
				r = document.createRange();
				r.setStart(p.offsetNode, p.offset);
			}
		} else if (document.caretRangeFromPoint)
			r = document.caretRangeFromPoint(x, y);
		if (!r) return;
		r.collapse(true);
		let sel = window.getSelection();
		sel.removeAllRanges();
		sel.addRange(r);
	}

	Vue.component('a2-text-editor', {
		template: `
<div class="a2-text-editor" :contenteditable="disabled ? 'false' : 'true'" :data-placeholder="placeholder"
	@input="sync" @keydown="keydown" @focus="focus" @paste="paste" @click="open" @load.capture="loaded" @error.capture="loaded"
	@dragstart="dragInside(true)" @dragend="dragInside(false)" @drop="drop"></div>
`,
		props: {
			item: Object,
			prop: String,
			placeholder: String,
			disabled: Boolean,
			delegates: Object
		},
		computed: {
			value() {
				return this.item[this.prop] || '';
			}
		},
		watch: {
			// own input is never re-rendered — the caret stays
			value(v) {
				if (v === this.md) return;
				this.render(v);
			}
		},
		methods: {
			loaded,
			imageUrl(src) {
				return hook(this, 'ImageUrl', src) || null;
			},
			render(md) {
				this.md = md;
				this.$el.innerHTML = toHtml(md, src => this.imageUrl(src), false);
				this.highlight();
			},
			highlight() {
				let hl = linkHighlight();
				if (!hl) return;
				for (let r of this.ranges)
					hl.delete(r);
				this.ranges = this.gone ? [] : linkRanges(this.$el);
				for (let r of this.ranges)
					hl.add(r);
			},
			// Ctrl/⌘+click opens an image or a link, as a click in the view does; a plain click edits (places the caret,
			// selects an image). A disabled editor cannot be edited, so any click opens. An image still uploading
			// (blob:, no data-src) has nothing to open yet
			open(ev) {
				if (!(ev.ctrlKey || ev.metaKey) && !this.disabled)
					return;
				let url = null;
				let t = ev.target;
				if (t.tagName === 'IMG') {
					if (t.getAttribute('data-src'))
						url = t.src;
				} else {
					let link = linkRanges(this.$el).find(r => hit(r, ev.clientX, ev.clientY));
					if (link)
						url = link.toString();
				}
				if (!url)
					return;
				ev.preventDefault();
				window.open(url, '_blank', 'noopener');
			},
			sync() {
				if (this.gone) return;
				let el = this.$el;
				// Chrome leaves a <br> in an emptied editor — :empty and the placeholder need it gone
				if (!el.textContent && !el.querySelector('img'))
					el.innerHTML = '';
				this.md = toMarkdown(el);
				this.highlight();
				if (this.value !== this.md)
					this.item[this.prop] = this.md;
			},
			focus() {
				// Ctrl+B → <b>, not <span style>
				document.execCommand('styleWithCSS', false, false);
			},
			keydown(ev) {
				if (!(ev.ctrlKey || ev.metaKey)) return;
				if (ev.key === 'Enter') {
					ev.preventDefault();
					hook(this, 'Submit');
				} else if (ev.code === 'KeyI' || ev.code === 'KeyU')
					ev.preventDefault(); // italic and underline would be lost on save
			},
			canUpload() {
				return !!(this.delegates && this.delegates.UploadImage);
			},
			paste(ev) {
				let dt = ev.clipboardData;
				if (!dt) return;
				ev.preventDefault();
				let files = this.canUpload() ? images(dt.files) : [];
				if (files.length)
					this.insertImages(files);
				else
					this.insertText(dt.getData('text/plain'));
			},
			// dragstart fires only for a drag that began inside the editor
			dragInside(on) {
				this.moving = on;
			},
			drop(ev) {
				if (this.moving) return; // a move inside the editor is the browser's
				ev.preventDefault();
				let dt = ev.dataTransfer;
				this.$el.focus();
				caretAt(ev.clientX, ev.clientY);
				let files = this.canUpload() ? images(dt.files) : [];
				if (files.length)
					this.insertImages(files);
				else
					this.insertText(dt.getData('text/plain'));
			},
			insertText(text) {
				// foreign HTML never enters
				if (text)
					document.execCommand('insertText', false, text);
			},
			insertImages(files) {
				let items = files.map(f => {
					let file = named(f);
					return { file, url: URL.createObjectURL(file) };
				});
				// execCommand — for the native undo
				let html = items.map(x => `<img src="${x.url}" alt="${escapeHtml(x.file.name)}" class="uploading">`).join('');
				document.execCommand('insertHTML', false, html);
				for (let x of items)
					this.upload(x);
			},
			async upload(x) {
				let src = null;
				try {
					src = await hook(this, 'UploadImage', x.file);
				} catch (err) {
					console.error(err);
				}
				let url = src ? this.imageUrl(src) : null;
				if (src && !url)
					console.error(`a2-text-editor: ImageUrl knows nothing of '${src}' returned by UploadImage`);
				// erased while uploading — nothing to do
				let img = Array.from(this.$el.querySelectorAll('img')).find(i => i.getAttribute('src') === x.url);
				if (img) {
					if (url) {
						img.setAttribute('data-src', src);
						img.src = url;
						img.classList.remove('uploading');
					} else
						img.remove();
				}
				URL.revokeObjectURL(x.url);
				// an attribute change fires no input
				this.sync();
			}
		},
		mounted() {
			// not reactive: own state of the DOM, never rendered
			this.md = '';
			this.moving = false;
			this.gone = false;
			this.ranges = [];
			this.render(this.value);
		},
		destroyed() {
			this.gone = true;
			this.highlight();
		}
	});

	Vue.component('a2-text-view', {
		template: `<div class="a2-text-view" v-html="html" @load.capture="loaded" @error.capture="loaded"></div>`,
		// shown, not written — value, as a2-blob-preview
		props: {
			value: String,
			delegates: Object
		},
		computed: {
			html() {
				return toHtml(this.value || '', src => hook(this, 'ImageUrl', src) || null, true);
			}
		},
		methods: {
			loaded
		}
	});
})();

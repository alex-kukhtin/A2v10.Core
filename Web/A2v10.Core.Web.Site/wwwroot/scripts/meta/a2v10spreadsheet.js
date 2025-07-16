(function (factory) {
	typeof define === 'function' && define.amd ? define(factory) :
	factory();
})((function () { 'use strict';

	const toColRef = (n) => n >= 26 ? String.fromCharCode(Math.floor(n / 26) + 64) + String.fromCharCode(n % 26 + 65) : String.fromCharCode(n + 65);
	const toPx = (n) => n + 'px';

	const rowHeaderWidth = 32;
	const columnHeaderHeigth = 23; // column header height - 1


	function styleHashCode(st) {
		if (!st) return '-';
		let b = st.Bold ? 'B' : '-';
		let i = st.Italic ? 'I' : '-';
		let fs = st.FontSize ? `FS${st.FontSize}` : '-';
		let a = st.Align ? st.Align[0] : '-';
		let va = st.VAlign ? st.VAlign[0] : '-';
		return `${b}:${i}:${fs}:${a}:${va}`;
	}

	class StyleProcessor {
		constructor(styles) {
			this.styles = styles;
		}

		findStyle(st) {
			let hash = styleHashCode(st);
			console.dir(st);
			return this.styles[hash] || null;
		}

		setStyle(st, prop, val) {

		}
	}

	Vue.component('a2-scroll-bar', {
		template: `
<div class="a2-sb" :class="sbClass">
	<button @click.stop=dec :disabled=decDisabled v-text=decLabel></button>
	<div class="sb-body" @click.self="clickBody" ref=body>
		<div class="thumb" @pointerdown.self.stop=thumbDown
			@pointerup.self.stop=thumbUp @pointermove.self.stop=thumbMove
			:style=thumbStyle :class="{horz: horz, vert: !horz }"></div>
	</div>
	<button @click.stop=inc :disabled=incDisabled v-text=incLabel></button>
</div>
`,
		props: {
			horz: Boolean,
			page: Number,
			pos: Number,
			min: Number,
			max: Number,
			setPos: Function
		},
		data() {
			return {
				tempClientSize: 0,
				moving: false,
				delta: 0,
				thumbMovePos: 0
			};
		},
		computed: {
			sbClass() {
				return this.horz ? 'horz' : 'vert';
			},
			thumbPos() {
				if (this.moving)
					return this.thumbMovePos;
				let step = (this.clientSize) / (this.size);
				return step * (this.pos - this.min);
			},
			size() {
				return this.max - this.min;
			},
			clientSize() {
				let s = this.tempClientSize;
				if (this.$refs.body)
					s = (this.horz ? this.$refs.body.clientWidth : this.$refs.body.clientHeight);
				return s;
			},
			thumbSize() {
				let sz = this.clientSize;
				return this.page * sz / this.size;
			},
			thumbStyle() {
				if (this.horz)
					return { left: toPx(this.thumbPos), width: toPx(this.thumbSize) };
				else
					return { top: toPx(this.thumbPos), height: toPx(this.thumbSize) };
			},
			decDisabled() {
				return this.pos <= this.min;
			},
			incDisabled() {
				return this.pos >= this.max - this.page;
			},
			decLabel() { return this.horz ? '◂' : '▴'; },
			incLabel() { return this.horz ? '▸' : '▾'; }
		},
		methods: {
			dec() {
				this.setPosCheck(this.pos - 1);
			},
			inc() {
				this.setPosCheck(this.pos + 1);
			},
			setPosCheck(np) {
				if (np < this.min)
					np = this.min;
				if (np > this.max - this.page)
					np = this.max - this.page;
				this.setPos(np);
			},
			clickBody(ev) {
				let dx = this.horz ? ev.offsetX : ev.offsetY;
				let np = 0;
				if (dx < this.thumbPos)
					np = this.pos - this.page;
				else
					np = this.pos + this.page;
				this.setPosCheck(np);
			},
			thumbDown(ev) {
				ev.target.setPointerCapture(ev.pointerId);
				this.delta = this.hors ? ev.offsetX : ev.offsetY;
				this.thumbMovePos = this.thumbPos;
				this.moving = true;
			},
			thumbMove(ev) {
				if (!this.moving) return;
				let rect = this.$refs.body.getBoundingClientRect();
				let tpos = this.horz ? ev.clientX - this.delta - rect.left : ev.clientY - this.delta - rect.top;
				if (tpos < 0) tpos = 0;
				let ths = this.thumbSize;
				let maxPos = this.horz ? rect.width - ths : rect.height - ths;
				if (tpos > maxPos) tpos = maxPos;
				this.thumbMovePos = tpos;
			},
			thumbUp(ev) {
				ev.target.releasePointerCapture(ev.pointerId);
				let pos = this.thumbMovePos;
				this.moving = false;
				let step = this.clientSize / (this.max - this.min);
				let np = Math.min(+ (pos / step).toFixed(), this.max - this.page);
				this.setPosCheck(np + this.min);
			}
		},
		mounted() {
			this.tempClientSize = (this.horz ? this.$refs.body.clientWidth : this.$refs.body.clientHeight);
		}
	});

	var spreadSheetCanvas = {
		data() {
			return {
				rType: '',
				rItem: -1,
				rPt: -1,
				rSize: -1,
				delta: 0,
			}
		},
		methods: {
			clearData() {
				this.rType = '';
				this.rItem = -1;
				this.rSize = 0;
				this.rPt = 0;
				this.delta = 0;
			},
			onUp(ev, cb) {
				ev.target.releasePointerCapture(ev.pointerId);
				ev.stopPropagation();
				if (this.rItem < 0)
					return;
				let ns = this.rPt - this.rSize;
				if (ns < 5)
					ns = 5;
				cb(ns);
				this.clearData();
			},
			hMouseDown(ev) {
				ev.stopPropagation();
				let ep = this.$parent.pointFromEvent(ev);
				let col = this.$parent.colFromPoint(ep.x);
				if (!col) return;
				ev.target.setPointerCapture(ev.pointerId);
				this.rType = 'C';
				this.rItem = col.col;
				this.rPt = col.left + col.width;
				this.rSize = col.left;
				this.delta = this.rPt - ev.clientX;
			},
			hMouseUp(ev) {
				this.onUp(ev, nw => {
					let col = this.$parent.getOrCreateColumn(this.rItem);
					Vue.set(col, 'Width', nw);
				});
			},
			hMouseMove(ev) {
				if (this.rItem < 0)
					return;
				this.rPt = ev.clientX + this.delta;
			},
			vMouseDown(ev) {
				ev.stopPropagation();
				let ep = this.$parent.pointFromEvent(ev);
				let row = this.$parent.rowFromPoint(ep.y);
				if (!row) return;
				ev.target.setPointerCapture(ev.pointerId);
				this.rType = 'R';
				this.rItem = row.row;
				this.rPt = row.top + row.height;
				this.rSize = row.top;
				this.delta = this.rPt - ev.clientY;
			},
			vMouseUp(ev) {
				this.onUp(ev, nh => {
					let row = this.$parent.getOrCreateRow(this.rItem);
					Vue.set(row, 'Height', nh);
				});
			},
			vMouseMove(ev) {
				if (this.rItem < 0)
					return;
				this.rPt = ev.clientY + this.delta;
			},
		},
		render(h) {
			let parent = this.$parent;
			let sh = parent.sheet;
			let cont = parent.$refs.container;
			if (!cont)
				return;
			let maxWidth = cont.clientWidth;
			let maxHeigth = cont.clientHeight;
			let elems = [];
			let self = this;
			let startX = parent.startX;

			let maxPos = { right: 0, bottom: 0 };

			function vLines() {
				let x = startX; // row header width
				for (const c of parent.renderedColumns()) {
					x += parent.colWidth(c);
					if (x >= maxWidth)
						break;
					let cls = 'vline';
					if (c == sh.Fixed.Columns - 1)
						cls += ' freeze';
					elems.push(h('div', { class: cls, style: { left: toPx(x), height: toPx(maxPos.bottom) } }));
				}
			}

			function hLines() {
				let y = columnHeaderHeigth;
				for (const r of parent.renderedRows()) {
					let rh = parent.rowHeight(r);
					y += rh;
					if (y >= maxHeigth)
						break;
					let cls = 'hline';
					if (r === sh.Fixed.Rows - 1)
						cls += ' freeze';
					elems.push(h('div', { class: cls, style: { top: toPx(y), width: toPx(maxPos.right) } }));
				}
			}

			function topHeader() {
				let x = startX; // row header width
				for (const c of parent.renderedColumns()) {
					let colRef = toColRef(c);
					let cw = parent.colWidth(c);
					let cls = 'thc';
					if (parent.isInSelection((sel, cell) => c >= sel.left && c <= sel.left + (cell?.ColSpan - 1 || 0)))
						cls += ' thc-sel';
					if (c === sh.Size.Columns - 1) {
						cls += ' last';
						cw += 1;
					}
					elems.push(h('div', { class: cls, style: { width: toPx(cw), left: toPx(x) } }, [colRef,
						h('div', {
							class: 'h-size no-me',
							on: { pointerdown: self.hMouseDown, pointerup: self.hMouseUp, pointermove: self.hMouseMove }
						})]));
					x += cw;
					if (x >= maxWidth)
						break;
				}
				maxPos.right = x - rowHeaderWidth - 1; /* without combo! */
			}

			function leftHeader() {
				let y = columnHeaderHeigth;
				for (const r of parent.renderedRows()) {
					let rh = parent.rowHeight(r);
					let cls = 'thr';
					if (parent.isInSelection((sel, cell) => r >= sel.top && r <= sel.top + (cell?.RowSpan - 1 || 0)))
						cls += ' thr-sel';
					if (r === sh.Size.Rows - 1) {
						cls += ' last';
						rh += 1;
					}
					elems.push(h('div', { class: cls, style: { height: toPx(rh), top: toPx(y) } }, ['' + (r + 1),
					h('div', {
						class: 'v-size no-me',
						on: { pointerdown: self.vMouseDown, pointerup: self.vMouseUp, pointermove: self.vMouseMove }
					})]));
					y += rh;
					if (y >= maxHeigth) {
						break;
					}
				}
				maxPos.bottom = y - columnHeaderHeigth - 1;
			}

			if (parent.headers) {
				elems.push(h('div', { class: 'r-top' }));
				topHeader();
				leftHeader();
			} else if (parent.gridLines) {
				console.dir('calc maxPos');
			}

			if (parent.gridLines) {
				vLines();
				hLines();
			}

			if (this.rItem >= 0) {
				if (this.rType == 'C')
					elems.push(h('div', { class: 'h-resize-line', style: { left: toPx(this.rPt) } }));
				else
					elems.push(h('div', { class: 'v-resize-line', style: { top: toPx(this.rPt) } }));
			}


			return h('div', { class: 'canvas' }, elems);
		}
	};

	var spreadSheetCells = {
		render(h) {
			let p = this.$parent;
			let sh = p.sheet;
			let cont = p.$refs.container;
			if (!cont)
				return;
			let maxWidth = cont.clientWidth;
			let maxHeigth = cont.clientHeight;
			let elems = [];
			let y = columnHeaderHeigth;
			const startX = p.startX;
			for (const r of p.renderedRows()) {
				let x = startX;
				let rh = this.$parent.rowHeight(r);
				for (const c of p.renderedColumns()) {
					let colRef = toColRef(c);
					let cw = p.colWidth(c);
					const cellRef = `${colRef}${r + 1}`;
					let cell = sh.Cells[cellRef];
					if (cell) {
						let nw = cw;
						let nh = rh;
						let cellCls = 'cell' + p.cellClass(cell);
						if (cell.ColSpan > 1 || cell.RowSpan > 1) {
							for (let sc = 1; sc < cell.ColSpan; sc++)
								nw += p.colWidth(sc + c);
							for (let sr = 1; sr < cell.RowSpan; sr++)
								nh += p.rowHeight(sr + r);
							cellCls += ' span';
							elems.push(h('div', {
								class: 'cell-ph',
								style: { left: toPx(x + 1), top: toPx(y + 1), width: toPx(nw - 1), height: toPx(nh - 1) },
							}));
						}
						elems.push(h('div', {
							class: cellCls,
							style: { left: toPx(x), top: toPx(y), width: toPx(nw + 1), height: toPx(nh + 1) },
							on: { pointerdown: (ev) => this.clickPh(c, r, cell, ev) }
						},
						cell.Content));
					}
					x += cw;
					if (x >= maxWidth)
						break;
				}
				y += rh;
				if (y >= maxHeigth)
					break;
			}
			return h('div', { class: 'cells' }, elems);
		},
		methods: {
			clickPh(c, r, cell, ev) {
				ev.preventDefault();
				ev.stopPropagation();
				this.$parent.$selectCell(c, r, cell);
			}
		}
	};

	var spreadSheetSelection = {
		render(h) {
			let sel = this.$parent.sheet.$selection;
			let ch = [];
			for (let i = 0; i < sel.length; i++) {
				let s = sel[i];
				let r = this.$parent.selRect(s);
				if (r && r.left >= 0 && r.top >= 0) {
					for (let cx = s.left + 1; cx < s.right; cx++)
						r.width += this.$parent.colWidth(cx);
					for (let rx = s.top + 1; rx < s.bottom; rx++)
						r.height += this.$parent.rowHeight(rx);
					let cls = 'sel';
					if (s.bottom > s.top + 1 || s.right > s.left + 1)
						cls += ' multiply';
					ch.push(h('div', { class: cls, style: { left: toPx(r.left), top: toPx(r.top), width: toPx(r.width + 1), height: toPx(r.height + 1) } }));
				}
			}
			return h('div', { class: 'selections' }, ch);
		}
	};

	var spreadSheetEdit = {
		methods: {
			blur(ev) {
				let p = this.$parent;
				p.endEdit(ev.target.innerText);
			}
		},
		render(h) {
			let p = this.$parent;
			let r = p.editRect;
			let cellRef = `${toColRef(r.c)}${r.r + 1}`;
			let cell = p.sheet.Cells[cellRef];
			return h('div', {
				class: 'input cell cell-edit no-me' + p.cellClass(cell),
				style: { left: toPx(r.l + 1), top: toPx(r.t + 1), width: toPx(r.w - 1), height: toPx(r.h - 1) },
				domProps: { contentEditable: true },
				on: { blur: this.blur }
			}, p.editText);
		},
		mounted() {
			this.$el.focus();
			this.$el.spellcheck = false;
		}
	};

	const toolbarTemplate = `
<div class="ss-toolbar">TOOLBAR
	<button @click="toggleBool('Bold')" :class="{checked: isChecked('Bold')}">B</button>
	<button @click="toggleBool('Italic')" :class="{checked: isChecked('Italic')}">I</button>
</div>
`;
	var spreadSheetToolbar = {
		template: toolbarTemplate,
		props: {
		},
		methods: {
			isChecked(prop) {
				return this.$parent.$getSelProp(prop);
			},
			toggleBool(prop) {
				this.$parent.sheet;
				this.$parent.$setSelProp(prop, !this.isChecked(prop));
				return;
			}
		}
	};

	const defaultColumWidth = 100;
	const defaultRowHeight = 23;

	const rowComboWidth = 75;

	function* enumerateSel(sel) {
		if (!sel || !sel.length) return;
		for (let sa of sel)
			for (let r = sa.top; r < sa.bottom; r++)
				for (let c = sa.left; c < sa.right; c++)
					yield `${toColRef(c)}${r + 1}`;
	}

	const spreadsheetTemplate = `
<div class="ss-container" :class="{editable}">
	<ss-toolbar v-if="editable" />
	<div class="ss-body" :key=updateCount ref=container tabindex=0
		@pointerup=pointerup @pointerdown=pointerdown @pointermove=pointermove
		@dblclick=dblclick @keydown.self.stop=keydown @wheel.prevent=mousewheel>
		<ss-canvas />
		<ss-rows-combo v-if="rowsCombo" />
		<ss-cells />
		<ss-selection />
		<ss-edit v-if=editing text=editText />
	</div>
	<a2-scroll-bar class="no-me" :horz=true :min="sheet.Fixed.Columns" :max="sheet.Size.Columns"
		:pos="scrollPos.x" :page="hScrollPageSize()"" :setPos=setPosX></a2-scroll-bar>
	<a2-scroll-bar class="no-me" :horz=false :min="sheet.Fixed.Rows" :max="sheet.Size.Rows"
		:pos="scrollPos.y" :page="vScrollPageSize()" :setPos=setPosY></a2-scroll-bar>
</div>
`;

	Vue.component('a2-spreadsheet', {
		template: spreadsheetTemplate,
		components: {
			'ss-canvas': spreadSheetCanvas,
			'ss-cells': spreadSheetCells,
			'ss-selection': spreadSheetSelection,
			'ss-edit': spreadSheetEdit,
			'ss-toolbar': spreadSheetToolbar
		},
		props: {
			sheet: Object,
			gridLines: { type: Boolean, default: true },
			headers: { type: Boolean, default: true },
			rowsCombo: Boolean,
			editable: { type: Boolean, default: true }
		},
		data() {
			return {
				updateCount: 0,
				scrollPos: { x: 0, y: 0 },
				editing: false,
				selecting: false,
				editRect: { l: 0, t: 0, w: 0, h: 0, c: '', r: -1 },
				editText: '',
				selStart: { c: 0, r: 0 }
			};
		},
		computed: {
			startX() {
				return rowHeaderWidth + (this.rowsCombo ? rowComboWidth : 0);
			}
		},
		methods: {
			rowFromPoint(pointY) {
				let y = columnHeaderHeigth;
				for (const r of this.renderedRows()) {
					let rh = this.rowHeight(r);
					if (pointY >= y && pointY <= (y + rh))
						return { row: r, top: y, height: rh };
					y += rh;
				}
				return null;
			},
			colFromPoint(pointX) {
				let x = this.startX;
				for (const c of this.renderedColumns()) {
					let cw = this.colWidth(c);
					if (pointX >= x && pointX <= (x + cw))
						return { col: c, left: x, width: cw };
					x += cw;
				}
				return null;
			},
			colWidth(c) {
				let col = this.sheet.Columns[toColRef(c)];
				return col ? col.Width : defaultColumWidth;
			},
			rowHeight(r) {
				let row = this.sheet.Rows[r + 1];
				return row ? row.Height : defaultRowHeight;
			},
			getOrCreateRow(r) {
				let row = this.sheet.Rows[r + 1];
				if (!row) {
					row = {};
					Vue.set(this.sheet.Rows, r + 1, row);
				}
				return row;
			},
			getOrCreateColumn(c) {
				let colRef = toColRef(c);
				let col = this.sheet.Columns[colRef];
				if (!col) {
					col = {};
					Vue.set(this.sheet.Columns, colRef, col);
				}
				return col;
			},
			selRect(s) {
				let xc = s.left; let xr = s.top;
				let y = columnHeaderHeigth;
				let rect = { left: 0, top: 0, height: 0, width: 0 };
				for (const r of this.renderedRows()) {
					let rh = this.rowHeight(r);
					if (r === xr) {
						rect.top = y; rect.height = rh;
						break;
					}
					else if (r > xr)
						break;
					y += rh;
				}
				let x = this.startX;
				for (const c of this.renderedColumns()) {
					let cw = this.colWidth(c);
					if (c === xc) {
						rect.left = x, rect.width = cw;
						break;
					}
					else if (c > xc)
						break;
					x += cw;
				}
				return rect;
			},
			pointFromEvent(ev) {
				let cont = this.$refs.container;
				let rect = cont.getBoundingClientRect();
				let y = ev.clientY - rect.top;
				let x = ev.clientX - rect.left;
				return { x, y };
			},
			setPosX(pos) {
				this.scrollPos.x = pos;
			},
			setPosY(pos) {
				this.scrollPos.y = pos;
			},
			renderedRows: function* () {
				let sh = this.sheet;
				for (let f = 0; f < sh.Fixed.Rows; f++)
					yield f;
				for (let f = this.scrollPos.y; f < sh.Size.Rows; /* maxRows ??*/ f++)
					yield f;
			},
			renderedColumns: function* () {
				let sh = this.sheet;
				for (let c = 0; c < sh.Fixed.Columns; c++)
					yield c;
				for (let c = this.scrollPos.x; c < sh.Size.Columns; /*maxColumns ?? */ c++)
					yield c;
			},
			setPos(c, r) {
				let sa = this.sheet.$selection;
				if (!sa || !sa.length) return;
				let sel = sa[0];
				let sh = this.sheet;
				sh.Fixed;
				let sz = sh.Size;
				if (c < 0) c = 0;
				if (c > sz.Columns - 1) c = sz.Columns - 1;
				if (r < 0) r = 0;
				if (r > sz.Rows - 1) r = sz.Rows - 1;
				sel.left = c; sel.right = c + 1;
				sel.top = r; sel.bottom = r + 1;
				this.fitScrollPos();
			},
			mousewheel(ev) {
				let deltaY = +(ev.deltaY / 100).toFixed();
				if (deltaY > 0)
					this.scrollPos.y = Math.min(this.scrollPos.y + deltaY, this.sheet.Size.Rows - this.vScrollPageSize());
				else
					this.scrollPos.y = Math.max(this.scrollPos.y + deltaY, this.sheet.Fixed.Rows);
			},
			keydown(ev) {
				let sa = this.sheet.$selection;
				if (!sa || !sa.length) return;
				let sel = sa[0];
				switch (ev.which) {
					case 37: /* left */
						this.setPos(sel.left - 1, sel.top);
						break;
					case 38: /* up */
						this.setPos(sel.left, sel.top - 1);
						break;
					case 39: /* right */
						this.setPos(sel.left + 1, sel.top);
						break;
					case 40: /* down */
						this.setPos(sel.left, sel.top + 1);
						break;
					case 34: /* pgdn */
						this.setPos(sel.left, sel.top + this.vScrollPageSize() + this.sheet.Fixed.Rows /*TODO last visible row*/);
						break;
					case 33: /* pgup */
						this.setPos(sel.left, sel.top - this.vScrollPageSize()/*TODO* first visible row*/);
						break;
					case 36: /* home */
						this.setPos(0, sel.top);
						break;
					case 35: /* end */
						this.setPos(this.sheet.Size.Columns - 1, sel.top);
						break;
					case 113: /* F2 */
						let sr = this.selRect(sel);
						this.doEdit({ col: sel.left, left: sr.left, width: sr.width },
							{ row: sel.top, top: sr.top, heigth: sr.height }
						);
						break;
					default:
						console.dir(ev.which);
						break;
				}
			},
			fitSpan(cp, rp) {
				if (!cp || !rp) return;
				let cellRef = `${toColRef(cp.col)}${rp.row + 1}`;
				let cell = this.sheet.Cells[cellRef];
				if (cell) {
					if (cell.ColSpan > 1)
						for (let c = 1; c < cell.ColSpan; c++)
							cp.width += this.colWidth(cp.col + c);
					if (cell.RowSpan > 1)
						for (let r = 1; r < cell.RowSpan; r++)
							rp.height += this.rowHeight(rp.row + r);
				}
			},
			dblclick(ev) {
				if (ev.srcElement.classList.contains('no-me'))
					return;
				let p = this.pointFromEvent(ev);
				let cp = this.colFromPoint(p.x);
				let rp = this.rowFromPoint(p.y);
				this.fitSpan(cp, rp);
				this.doEdit(cp, rp);
			},
			setProp(c, r, cb, create) {
				let cellRef = `${toColRef(c)}${r + 1}`;
				let cell = this.sheet.Cells[cellRef];
				if (!cell && create) {
					cell = {};
					Vue.set(this.sheet.Cells, cellRef, cell);
				}
				if (cell)
					cb(cell);
			},
			endEdit(val) {
				let r = this.editRect;
				let cellRef = `${toColRef(r.c)}${r.r + 1}`;
				let cell = this.sheet.Cells[cellRef];
				if (!cell) {
					if (!val) return;
					cell = { Content: val };
					Vue.set(this.sheet.Cells, cellRef, cell);
				}
				Vue.set(cell, 'Content', val);
			},
			startEdit(c, r) {
				return { control: 'editor' };
			},
			cancelEdit() {
				this.editing = false;
				this.editText = '';
			},
			doEdit(cp, rp) {
				if (!cp || !rp) return;
				let res = this.startEdit(toColRef(cp.col), rp.row);
				if (!res)
					return;
				let er = this.editRect;
				er.l = cp.left;
				er.t = rp.top;
				er.w = cp.width;
				er.h = rp.height;
				er.c = cp.col;
				er.r = rp.row;
				let c = this.sheet.Cells[`${toColRef(cp.col)}${rp.row + 1}`];
				if (c)
					this.editText = c.Content;
				else
					this.editText = '';
				this.editing = true;

			},
			$selectCell(c, r, cell) {
				let sht = this.sheet;
				let sa = sht.$selection;
				this.selecting = true;
				sa.length = 0;
				let sp = { left: c, top: r, right: c + (cell.ColSpan || 1), bottom: r + (cell.RowSpan || 1)};
				sa.push(sp);
			},
			pointerdown(ev) {
				if (ev.srcElement.classList.contains('no-me'))
					return;
				ev.target.setPointerCapture(ev.pointerId);
				this.cancelEdit();
				this.selecting = true;
				let sht = this.sheet;
				let sa = sht.$selection;
				let p = this.pointFromEvent(ev);
				if (p.x < this.startX) {
					let rp = this.rowFromPoint(p.y);
					sa.length = 0;
					let sp = { left: 0, top: rp.row, right: sht.Size.Columns + 1, bottom: rp.row + 1 };
					sa.push(sp);
				}
				else if (p.y < columnHeaderHeigth) {
					let cp = this.colFromPoint(p.x);
					sa.length = 0;
					let sp = { left: cp.col, top: 0, right: cp.col + 1, bottom: sht.Size.Rows + 1 };
					sa.push(sp);
				}
				else {
					let sp = this.createSelRect(p.x, p.y);
					if (sp) {
						sa.length = 0;
						sa.push(sp);
						this.selStart.x = sp.left;
						this.selStart.y = sp.top;
					}
				}
			},
			pointermove(ev) {
				if (!this.selecting) return;
				if (ev.srcElement.classList.contains('no-me'))
					return;
				let p = this.pointFromEvent(ev);
				let sa = this.sheet.$selection;
				if (!sa || !sa.length) return;
				let sel = sa[0];
				if (p.x < this.startX) {
					this.rowFromPoint(p.y);
					console.dir('select rows');
				}
				else if (p.y < columnHeaderHeigth) {
					this.colFromPoint(p.x);
					console.dir('select columns');
				}
				else {
					let sp = this.createSelRect(p.x, p.y);
					if (sp.left == this.selStart.x) {
						sel.left = sp.left;
						sel.right = sp.right;
					} if (sp.left < this.selStart.x)
						sel.left = sp.left;
					else
						sel.right = sp.right;
					if (sp.top == this.selStart.y) {
						sel.top = sp.top;
						sel.bottom = sp.bottom;
					} else if (sp.top < this.selStart.y)
						sel.top = sp.top;
					else
						sel.bottom = sp.bottom;
					this.fitScrollPos();
				}
			},
			pointerup(ev) {
				ev.target.setPointerCapture(ev.pointerId);
				this.selecting = false;
				this.selStart.x = 0;
				this.selStart.y = 0;
				this.fitScrollPos();
			},
			fitScrollPos() {
				let sa = this.sheet.$selection;
				if (!sa || !sa.length) return;
				let sel = sa[0];
				let cont = this.$refs.container;
				let cWidth = cont.clientWidth - 1; //- rowHeaderWidth;
				let cHeight = cont.clientHeight - 1; //- columnHeaderHeigth;
				let fix = this.sheet.Fixed;
				let sp = this.scrollPos;
				let sr = null;
				if (sp.y > fix.Rows && sel.top < sp.y)
					sp.y = Math.max(sel.top, fix.Rows);
				else {
					sr = sr || this.selRect(sel);
					let b = sr.top + sr.height + 1;
					if (b > cHeight)
						sp.y += 1;
				}
				if (sp.x > fix.Columns && sel.left < sp.x)
					sp.x = Math.max(sel.left, fix.Columns);
				else {
					sr = sr || this.selRect(sel);
					let r = sr.left + sr.width + 1;
					if (r > cWidth)
						sp.x += 1;
				}
			},
			createSelRect(x, y) {
				let rp = this.rowFromPoint(y);
				let cp = this.colFromPoint(x);
				if (cp && rp) {
					let cellRef = `${toColRef(cp.col)}${rp.row + 1}`;
					let cell = this.sheet.Cells[cellRef];
					return { left: cp.col, top: rp.row, right: cp.col + 1 + (cell?.ColSpan - 1 || 0), bottom: rp.row + 1 + (cell?.RowSpan - 1 || 0) };
				}
				return null;
			},
			isInSelection(cb, cell) {
				return this.sheet.$selection.some(c => cb(c, this.sheet.Cells[`${toColRef(c.left)}${c.top + 1}`]));
			},
			cellClass(cell) {
				if (!cell || !cell.Style) return '';
				let st = this.sheet.Styles[cell.Style];
				if (!st) return '';
				let c = '';
				if (st.Bold)
					c += ' bold';
				if (st.Italic)
					c += ' italic';
				if (st.Align)
					c += ` text-${st.Align.toLowerCase()}`;
				return c;
			},
			hScrollPageSize() {
				let cont = this.$refs.container;
				if (!cont)
					return 0;
				let x = this.startX;
				let max = cont.clientWidth;
				let cols = 0;
				for (let c of this.renderedColumns()) {
					x += this.colWidth(c);
					if (x > max)
						break;
					else if (c >= this.sheet.Fixed.Columns)
						cols += 1;
				}
				//console.dir(cols);
				return cols;
			},
			vScrollPageSize() {
				let cont = this.$refs.container;
				if (!cont)
					return 0;
				let y = columnHeaderHeigth;
				let max = cont.clientHeight;
				let rows = 0;
				for (let r of this.renderedRows()) {
					y += this.rowHeight(r);
					if (y > max)
						break;
					else if (r >= this.sheet.Fixed.Rows)
						rows += 1;
				}
				return rows;
			},
			$getSelProp(prop) {
				let sa = this.sheet.$selection;
				if (!sa || !sa.length) return false;
				let sel = sa[0];
				let cellRef = `${toColRef(sel.left)}${sel.top+1}`;
				let cell = this.sheet.Cells[cellRef];
				if (!cell || !cell.Style) return false;
				let style = this.sheet.Styles[cell.Style];
				if (!style) return false;
				return style[prop] || false;
			},
			$setSelProp(prop, val) {
				let sel = this.sheet.$selection;
				for (let cr of enumerateSel(sel)) {
					let cell = this.sheet.Cells[cr];
					if (!cell) {
						cell = { Content: val };
						Vue.set(this.sheet.Cells, cr, cell);
						cell = this.sheet.Cells[cr];
					}
					console.dir(cell.Style);
					this.__sp.findStyle(cell.Style);
					console.dir(styleHashCode(cell.Style));
				}
				return true;
			}
		},
		mounted() {
			this.updateCount += 1;
			let sp = { left: 0, top: 0, right: 1, bottom: 1 };
			this.sheet.$selection.push(sp);
			this.scrollPos.x = this.sheet.Fixed.Columns;
			this.scrollPos.y = this.sheet.Fixed.Rows;
			this.__ro = new ResizeObserver(x => {
				this.updateCount += 1;
				this.fitScrollPos();
			});
			this.__ro.observe(this.$el);
			this.__sp = new StyleProcessor(this.sheet.Styles);
			for (let s of Object.keys(this.sheet.Styles)) {
				console.log(s, styleHashCode(this.sheet.Styles[s]));
			}
		},
		beforeDestroy() {
			if (this.__ro)
				this.__ro.unobserve(this.$el);
		}
	});

}));
//# sourceMappingURL=a2v10spreadsheet.js.map

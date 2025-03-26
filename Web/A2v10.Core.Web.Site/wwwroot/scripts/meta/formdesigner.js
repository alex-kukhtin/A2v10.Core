(function (factory) {
	typeof define === 'function' && define.amd ? define(factory) :
	factory();
})((function () { 'use strict';

	const toolboxItemTemplate = `
<li class="fd-tbox-item" :draggable="true" @dragstart.stop=dragStart>
	<i class="ico ico-grid" />
	<span v-text=label />
</li>
`;

	var toolboxItem = {
		template: toolboxItemTemplate,
		props: {
			icon: String,
			label: String,
			item: Object,
			cont: Object
		},
		methods: {
			dragStart(ev) {
				console.dir(this.cont);
				this.cont.select(this.item);
				ev.dataTransfer.effectAllowed = "move";
			}
		}
	};

	const toolboxTemplate = `
<div class="fd-toolbox">
	<details open>
		<summary>Form Controls</summary>
		<ul>
			<toolboxitem v-for="(f, ix) in components" :key=ix :item=f :label=f.Is :cont=cont />
		</ul>
	</details>
	<details open>
		<summary>Data</summary>
		<ul>
			<toolboxitem v-for="(f, ix) in fields" :key=ix :item=f :label=f.Data :cont=cont />
		</ul>
	</details>
</div>
`;

	var toolboxElem = {
		template: toolboxTemplate,
		components: {
			toolboxitem: toolboxItem
		},
		props: {
			fields: Array,
			components: Array,
			cont: Object
		},
		computed: {
		},
		methods: {

		}
	};

	const propsheetTemplate = `
<div class="fd-propsheet">
	<div class="fd-item-is" v-text="item.Is" />
	<table>
		<tr>
			<td colspan=2 class="fd-ps-header">General</td>
		</tr>
		<tr v-for="(p, ix) in itemProps" :key=ix>
			<td v-text="p.name" />
			<td>
				<input v-model.lazy.trim="p.value" />
			</td>
		</tr>
		<tr v-if="item.Grid">
			<td colspan=2 class="fd-ps-header">Grid</td>
		</tr>
		<tr v-if="item.Grid" v-for="(p, ix) in gridProps" :key="'g:' + ix">
			<td v-text="p.name"/>
			<td>
				<input v-model.lazy.trim="p.value" type=number />
			</td>
		</tr>
		<tr v-if="item.Command">
			<td colspan=2 class="fd-ps-header">Command</td>
		</tr>
		<tr v-if="item.Command" v-for="(p, ix) in commandProps" :key="'c:' + ix">
			<td v-text="p.name"/>
			<td>
				<input v-model.lazy.trim="p.value" />
			</td>
		</tr>
	</table>
</div>
`;

	// TODO: ������������� ������� Dialog.Label => Dialog.Title?
	const PROP_MAP = {
		Grid: ['Rows', 'Columns', "Height"],
		TextBox: ["Data", 'Label', "Width"],
		DatePicker: ["Data", 'Label', "Width"],
		Selector: ["Data", 'Label', "Width"],
		DataGrid: ["Data", 'Height'],
		CLabel: ["Label"],
		DataGridColumn: ["Data", 'Label'],
		Toolbar: [],
		Pager: ['Data'],
		Dialog: ['Label', 'Width', 'Height', "Data"],
		Page: ['Label', "Data"],
		Button: ['Label', "Parameter"],
		GRID_PROPS: ['Row', 'Col', 'RowSpan', 'ColSpan'],
		COMMAND_PROPS: ['Command', 'Argument', 'Url']
	};

	var propsheetElem = {
		template: propsheetTemplate,
		props: {
			item: Object
		},
		computed: {
			itemProps() {
				if (!this.item) return [];
				const type = this.item.Is;
				return this.getProps(PROP_MAP[type], this.item);
			},
			gridProps() {
				let g = this.item.Grid;
				if (!g) return [];
				return this.getProps(PROP_MAP['GRID_PROPS'], this.item.Grid);
			},
			commandProps() {
				let g = this.item.Command;
				if (!g) return [];
				return this.getProps(PROP_MAP['COMMAND_PROPS'], this.item.Command);
			}
		},
		methods: {
			getProps(props, item) {
				if (!props) return [];
				return props.map(p => {
					const r = {
						name: p,
						get value() { return item[p] || ''; },
						set value(v) { Vue.set(item, p, v); }
					};
					return r;
				});
			}
		}
	};

	const taskpadTemplate$1 = `
<div class="fd-taskpad">
	<ul class="fd-tabbar">
		<li :class="{active: activeTab === 'tbox'}" @click.stop.prevent="activeTab = 'tbox'">Toolbox</li>
		<li :class="{active: activeTab === 'props'}" @click.stop.prevent="activeTab = 'props'">Properties</li>
	</ul>
	<toolbox v-if="activeTab === 'tbox'" :fields=fields :cont=cont :components=components />
	<propsheet v-if="activeTab === 'props'" :item=item  />
</div>
`;

	var taskpad = {
		template: taskpadTemplate$1,
		props: {
			item: Object,
			fields: Array,
			components: Array,
			cont: Object
		},
		components: {
			'toolbox': toolboxElem,
			'propsheet': propsheetElem
		},
		data() { 
			return {
				activeTab: 'tbox'
			};
		},
		computed: {
			props() {
				return this.item ? this.item.Props : [];
			}
		},
		methods: {
		}
	};

	const toolbarTemplate = `
<div class="toolbar fd-toolbar">
	<button class="btn btn-tb btn-icon">
		<i class="ico ico-save-outline" />
	</button>
	<button class="btn btn-tb btn-icon">
		<i class="ico ico-clear" />
	</button>
	<div class="divider" />
	<button class="btn btn-tb btn-icon">
		<i class="ico ico-reload" />
	</button> 
</div>
`;

	var toolbar$1 = {
		template: toolbarTemplate,
		props: {
			form: Object	
		}
	};

	const gridPlaceholder = `
<div class="fd-grid-ph" :style="style"
	@drop=drop @dragover=dragOver @dragenter=dragEnter @dragleave=dragLeave
		:class="{hover}"/>
`;

	var gridPlaceholder$1 = {
		template: gridPlaceholder,
		props: {
			row: Number,
			col: Number,
			cont: Object
		},
		data() {
			return {
				hover: false
			};
		},
		computed: {
			style() {
				return `grid-area: ${this.row} / ${this.col}`;
			}
		},
		methods: {
			dragOver(ev) {
				if (!this.cont.canDrop('grid'))
					return;
				ev.preventDefault();	
			},
			dragEnter(ev) {
				//console.dir("drag enter");
				this.hover = true;
			},
			dragLeave(ev) {
				//console.dir("drag leave");
				this.hover = false;
			},
			drop(ev) {
				this.hover = false;
				//let dropData = ev.dataTransfer.getData('text/plain');
				this.cont.drop({row: this.row, col: this.col, grid: this.$parent.item});
			}
		}
	};

	var control = {
		props: {
			item: Object,
			cont: Object,
		},
		computed: {
			controlStyle() {
				return {
					width: this.item.Width || ''
				};
			}
		}
	};

	const textBoxTemplate = `
<div class="control-group" :style=controlStyle >
<label v-text="item.Label" v-if="item.Label" />
	<div class="input-group">
		<span v-text="item.Data" class="input" />
	</div>
</div>
`;

	var textBox = {
		template: textBoxTemplate,
		extends: control
	};

	const selectorTemplate = `
<div class="control-group" :style=controlStyle >
<label v-text="item.Label" v-if="item.Label"/>
	<div class="input-group">
		<span v-text="item.Data" class="input" />
		<a>
			<i class="ico ico-search" />
		</a>
	</div>
</div>
`;

	var selector = {
		template: selectorTemplate,
		extends: control
	};

	const datePickerTemplate = `
<div class="control-group" :style=controlStyle >
	<label v-text="item.Label" v-if="item.Label"/>
	<div class="input-group">
		<span v-text="item.Data" class="input text-center"/>
		<a>
			<i class="ico ico-calendar" />
		</a>
	</div>
</div>
`;

	var datePicker = {
		template: datePickerTemplate,
		extends: control
	};

	var layoutItem = {
		props: {
			item: Object,
			cont: Object	
		},
		methods: {
			select() {
				this.cont.select(this.item);
			}
		},
		computed: {
			selected() {
				return this.cont.isActive(this.item);
			}
		}
	};

	const dataGridColumnTemplate = `
<div class="fd-datagrid-column" @click.stop.prevent="select" :class="{ selected }"
	:draggable=true @dragstart.stop=dragStart>
	<div v-text="item.Label" class="label" />
	<div v-text="item.Data" class="column" />
</div>
`;

	var dataGridColumn = {
		template: dataGridColumnTemplate,
		extends: layoutItem,
		methods: {
			dragStart(ev) {
				console.dir('drag start column');
				this.cont.select(this.item);
				ev.dataTransfer.effectAllowed = "move";
				ev.dataTransfer.setData('text/plain', JSON.stringify({ row: this.row, col: this.col }));
			}
		}
	};

	const dataGridTemplate = `
<div class="fd-datagrid" @dragover=dragOver @drop=drop :style=elemStyle >
	<DataGridColumn v-for="(c, ix) in item.Items" :item=c :key=ix :cont=cont />
</div>
`;

	var datagrid = {
		template: dataGridTemplate,
		props: {
			item: Object,
			cont: Object
		},
		components: {
			'DataGridColumn': dataGridColumn
		},
		methods: {
			dragOver(ev) {
				ev.preventDefault();
			},
			drop(ev) {
				alert('drop data grid');
			}
		},
		computed: {
			elemStyle() {
				return {
				}
			}
		}
	};

	var pager = {
		template: `<div class="a2-pager">
		<button>
			<i class="ico ico-chevron-left" />
		</button>
		<button class="active">1</button>
		<button>2</button>
		<button>3</button>
		<button>4</button>
		<button>5</button>
		<span class="a2-pager-dots">...</span>
		<button>8</button>
		<button>
			<i class="ico ico-chevron-right" />
		</button>
		<div class="a2-pager-divider" />
		<span class="a2-pager-title">items: <b>1</b>-<b>20</b> of <b>500</b></span>
	</div>`,
		props: {
			item: Object,
			cont: Object	
		}
	};

	const buttonTemplate$1 = `
<button class="btn btn-tb" @click.stop.prevent="select" :class="{ selected }" :draggable=true
		@dragstart.stop=dragStart >
	<i class="ico" :class=icon />
	<span v-if="item.Label" v-text="item.Label" />	
</button>
`;

	var button$1 = {
		template: buttonTemplate$1,
		extends: layoutItem,
		computed: {
			icon() {
				switch (this.item.Command.Command) {
					case 'Edit': return 'ico-edit';
					case 'New': return 'ico-add';
					case 'Create': return 'ico-add';
					case 'Delete': return 'ico-clear';
					case 'Reload': return 'ico-reload';
				}
				return 'ico-menu';
			}
		},
		methods: {
			dragStart(ev) {
				console.dir('drag start button');
				this.cont.select(this.item);
				ev.dataTransfer.effectAllowed = "move";
				ev.dataTransfer.setData('text/plain', this.item.Is);
			}
		}
	};

	const alignerTemplate = `
<div class="aligner" @click.stop.prevent="select" :class="{ selected }" :draggable=true
		@dragstart.stop=dragStart >
</div>
`;

	var aligner = {
		template: alignerTemplate,
		extends: layoutItem,
		methods: {
			dragStart(ev) {
				console.dir('drag start aligner');
				this.cont.select(this.item);
				ev.dataTransfer.effectAllowed = "move";
				ev.dataTransfer.setData('text/plain', this.item.Is);
			}
		}
	};

	function is2icon(is) {
		switch (is) {
			case 'SearchBox': return 'ico-search';
		}
		return '';
	}

	const inputControlTemplate = `
<div class="control-group" :style=controlStyle @click=itemClick>
<label v-text="item.Label" v-if="item.Label"/>
	<div class="input-group">
		<span v-text="item.Data" class="input" />
		<a v-if="icon">
			<i class="ico" :class="icon" />
		</a>
	</div>
</div>
`;

	const searchBox = {
		template: inputControlTemplate,
		extends: control,
		computed: {
			icon() { return is2icon(this.item.Is); }
		},
		methods: {
			itemClick(ev) {
				// todo: check if toolbar
				ev.preventDefault();
				ev.stopPropagation();
				this.cont.select(this.item);
			}
		}
	};


	var inputControls = {
		searchBox
	};

	var toolbar = {
		template: `<div class="toolbar" @dragover=dragOver @drop=drop >
		<component :is="item.Is" v-for="(item, ix) in item.Items" :item="item" :key="ix" :cont=cont />
	</div>`,
		extends: control,
		components: {
			'Button': button$1,
			'Aligner': aligner,
			'TextBox': textBox,
			'SearchBox': inputControls.searchBox
		},
		methods: {
			dragOver(ev) {
				ev.preventDefault();
			},
			drop(ev) {
				alert('drop toolbar');
			}
		}
	};

	var label = {
		template: '<label v-text="item.Label" />',
		extends: control
	};

	const gridItem = `
<div class="fd-grid-item" :draggable="true"
	@dragstart.stop=dragStart @dragend=dragEnd
	:style="style" @click.stop.prevent=select :class="{ selected }">
		<div class="handle" v-if=hasHandle></div>
		<component :is="item.Is" :item="item" :cont="cont" />
</div>
`;

	var gridItem$1 = {
		template: gridItem,
		props: {
			item: Object,
			cont: Object
		},
		components: {
			'TextBox': textBox,
			'Selector': selector,
			'DatePicker': datePicker,
			'DataGrid': datagrid,
			'CLabel': label, 
			'Pager': pager,
			'Toolbar': toolbar
		},
		computed: {
			grid() {
				return this.item.Grid || {};
			},
			row() {
				return this.grid.Row || '';
			},
			col() {
				return this.grid.Col || '';
			},
			rowSpan() {
				return this.grid.RowSpan || '';
			},
			colSpan() {
				return this.grid.ColSpan || '';
			},
			style() {
				let row = this.row;
				if (this.rowSpan)
					row += `/ span ${this.rowSpan}`;
				let col = this.col;
				if (this.colSpan)
					col += `/ span ${this.colSpan}`;
				return {
					gridRow: row,
					gridColumn: col,
					height: this.item.Height || ''
				};
			},
			selected() {
				return this.cont.isActive(this.item);
			},
			hasHandle() {
				return this.item.Is == 'DataGrid' || this.item.Is === "Toolbar";
			}
		},
		methods: {
			select() {
				this.cont.select(this.item);
			},
			dragStart(ev) {
				console.dir('drag start');
				this.cont.select(this.item);
				ev.dataTransfer.effectAllowed = "move";
				ev.dataTransfer.setData('text/plain', JSON.stringify({ row: this.row, col: this.col}));
			},
			dragEnd() {
				console.dir('drag end');
			}
		}
	};

	const gridTemplate = `
<div class="fd-elem-grid grid" @click.stop=select :style=gridStyle :class="{selected}">
	<template v-for="row in rows">
		<fd-grid-ph v-for="col in cols" :row=row :col="col" ref=ph
			:key="row + ':' + col" :cont=cont />
	</template>
	<fd-grid-item v-for="(itm, ix) in item.Items" :item=itm :key=ix :cont=cont />
</div>
`;

	var gridElem = {
		name: 'grid',
		extends: layoutItem,
		template: gridTemplate,
		components: {
			'fd-grid-ph': gridPlaceholder$1,
			'fd-grid-item': gridItem$1
		},
		props: {
			item: Object,
			cont: Object
		},
		computed: {
			props() {
				return this.item.Props || {};
			},
			cols() {
				if (!this.props.Columns) return 1;
				return this.props.Columns.split(' ').map((c, ix) => ix + 1);
			},
			rows() {
				if (!this.props.Rows) return 1;
				return this.props.Rows.split(' ').map((r, ix) => ix + 1);
			},
			gridStyle() {
				return {
					gridTemplateColumns: this.props.Columns || 'auto',
					gridTemplateRows: this.props.Rows || 'auto',
					height: this.item.Height || ''
				}
			},
		}
	};

	var lineElem = {
		template: '<div class="line" @click.stop.prevent=select :class="{selected}"><hr></div>',
		extends: layoutItem
	};

	const buttonTemplate = `
<button @click.stop.prevent="select" class="btn a2-inline" :class="{ selected, 'btn-primary': item.Primary }" :draggable=true
	@dragstart.stop=dragStart v-text="item.Label">
</button>
`;

	var button = {
		template: buttonTemplate,
		extends: layoutItem,
		computed: {
		},
		methods: {
			dragStart(ev) {
				console.dir('drag start button');
				this.cont.select(this.item);
				ev.dataTransfer.effectAllowed = "move";
				ev.dataTransfer.setData('text/plain', this.item.Is);
			}
		}
	};

	var dlgButtons = {
		template: `<div class="modal-footer" @dragover=dragOver @drop=drop >
		<component :is="itm.Is" v-for="(itm, ix) in elems"
			:item="itm" :key="ix" :cont=cont />
	</div>`,
		extends: control,
		props: {
			elems: Array
		},
		components: {
			'Button': button,
		},
		methods: {
			dragOver(ev) {
				ev.preventDefault();
			},
			drop(ev) {
				alert('drop toolbar');
			}
		}
	};

	const taskpadTemplate = `
<div class="fd-elem-taskpad">
	TASKPAD
</div>
`;

	var frmTaskpad = {
		template: taskpadTemplate,
		props: {
			item: Object,
			cont: Object
		}
	};

	const containerTemplate = `
<div class="fd-container" @keyup.self=keyUp tabindex=0 >
	<fd-toolbar></fd-toolbar>
	<fd-taskpad :item=selectedItem :fields=fields :cont=cont :components=components />
	<div class="fd-main" @click.stop.stop=clickBody>
		<div class=fd-body  @click.stop.stop=clickBody :class="bodyClass" :style="bodyStyle">
			<div v-if="isDialog" class="modal-header">
				<span class="modal-title" v-text="form.Label"/>
				<button tabindex="-1" class="btnclose">✕</button>
			</div>
			<div class="fd-content">
				<component v-for="(itm, ix) in form.Items" :key="ix" :is="itm.Is"
					:item="itm" :cont=cont />
			</div>
			<component :is="form.Taskpad.Is" :item="form.Taskpad" :cont=cont v-if="form.Taskpad" />
			<dlg-buttons v-if="isDialog" :elems="form.Buttons" :cont=cont />
		</div>
	</div>
</div>
`;

	function isContainer(isElem) {
		return isElem === 'Grid';
	}

	Vue.component('Grid', gridElem);

	Vue.component('fd-container', {
		template: containerTemplate,
		components: {
			'fd-toolbar': toolbar$1,
			'fd-taskpad': taskpad,
			'dlg-buttons': dlgButtons,
			'HLine': lineElem,
			'Taskpad': frmTaskpad
		},
		props: {
			form: Object,
			fields: Array,
			components: Array
		},
		data() {
			return {
				selectedItem: null
			};
		},
		computed: {
			cont() {
				return {
					select: this.$selectItem,
					drop: this.$dropItem,
					isActive: (itm) => itm === this.selectedItem,
					canDrop: this.$canDrop
				}
			},
			bodyClass() {
				return this.form.Is.toLowerCase();
			},
			bodyStyle() {
				let el = {};
				if (this.isDialog)
					el.width = this.form.Width;
				return el;
			},
			isDialog() {
				console.dir(this.form);
				return this.form.Is === 'Dialog';
			}
		},
		methods: {
			clickBody() {
				this.selectedItem = this.form;	
			},
			keyUp(ev) {
				console.dir(ev.which);
				switch (ev.which) {
					case 46: /* del */ this.deleteItem();
						break;
				}
			},
			deleteItem() {
				if (!this.selectedItem) return;
				let g = this.findGridByItem(this.selectedItem);
				if (!g || g.Is !== 'Grid') return;
				let ix = g.Items.indexOf(this.selectedItem);
				if (ix < 0) return;
				g.Items.splice(ix, 1);
				this.selectedItem = this.form;
			},
			findGridByItem(tf) {
				function findInContainer(el, tf) {
					if (!el || !el.Items) return null;
					for (let i = 0; i < el.Items.length; i++) {
						let x = el.Items[i];
						if (x === tf) return el;
						if (!isContainer(x.Is))
							continue;
						let res = findInContainer(x, tf);
						if (res) return res;	
					}
					return null;
				}
				return findInContainer(this.form, tf);
			},
			$selectItem(item) {
				this.selectedItem = item;
			},
			$canDrop(target) {
				let si = this.selectedItem;
				if (!si) return false;
				console.dir(si.Is);
				if (target === 'grid')
					return si.Is !== 'Button' && si.Is !== 'DataGridColumn';
				return true;
			},
			$dropItem(rc) {
				if (!this.selectedItem) return;
				console.dir(this.selectedItem);
				console.dir(rc);

				if (!this.selectedItem.row && !this.selectedItem.col) {
					let no = Object.assign({}, this.selectedItem);
					no.Items = [];
					no.row = rc.row;	
					no.col = rc.col;	
					rc.grid.Items.push(no);
					this.selectedItem = no;
					return;
				}

				// selectedItem может быть новым элементом	
				let fg = this.findGridByItem(this.selectedItem);
				if (fg && fg.Is === 'Grid' && fg !== rc.grid) {
					let ix = fg.Items.indexOf(this.selectedItem);
					fg.Items.splice(ix, 1);
					rc.grid.Items.push(this.selectedItem);
				}
				this.selectedItem.row = rc.row;
				this.selectedItem.col = rc.col;			
			}
		},
		mounted() {
			this.selectedItem = this.form;
		}
	});

}));
//# sourceMappingURL=formdesigner.js.map

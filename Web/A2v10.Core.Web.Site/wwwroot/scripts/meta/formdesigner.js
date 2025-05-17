(function (factory) {
	typeof define === 'function' && define.amd ? define(factory) :
	factory();
})((function () { 'use strict';

	const toolboxItemTemplate = `
<li class="fd-tbox-item" :draggable="true" @dragstart.stop=dragStart>
	<i class="ico ico-rename" />
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

	const defaultControls = [
		{ Is: 'Grid', Props: {Rows: 'auto auto', Columns: 'auto auto'} },
		{ Is: 'Button' },
		{ Is: 'Panel' },
	];

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
			cont: Object
		},
		computed: {
			components() {
				return defaultControls;
			}
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
		<tr v-for="(p, ix) in itemProps" :key="'i:'+ix">
			<td v-text="p.name" />
			<td>
				<input v-model.lazy.trim="p.value" />
			</td>
		</tr>
		<tr v-for="(p, ix) in otherProps" :key="'o:'+ix">
			<td v-text="p.name" />
			<td>
				<input v-model.lazy.trim="p.value" />
			</td>
		</tr>
		<tr v-if="hasGrid">
			<td colspan=2 class="fd-ps-header">Grid</td>
		</tr>
		<tr v-if="hasGrid" v-for="(p, ix) in gridProps" :key="'g:' + ix">
			<td v-text="p.name"/>
			<td>
				<input v-model.lazy.trim="p.value" type=number />
			</td>
		</tr>
		<tr v-if="hasCommand">
			<td colspan=2 class="fd-ps-header">Command</td>
		</tr>
		<tr v-if="hasCommand" v-for="(p, ix) in commandProps" :key="'c:' + ix">
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
		Grid: ["Height", "CssClass"],
		TextBox: ['Data', 'Label', 'Width'],
		ComboBox: ['Data', 'Label', 'Width'],
		SearchBox: ['Data', 'Label', 'Width'],
		Static: ['Data', 'Label', 'Width'],
		DatePicker: ['Data', 'Label', 'Width'],
		PeriodPicker: ['Data', 'Label', 'Width'],
		Selector: ['Data', 'Label', 'Width'],
		Header: ['Data', 'Label'],
		DataGrid: ['Data', 'Height'],
		Label: ['Label'],
		Panel: ["Label"],
		DataGridColumn: ['Data', 'Label'],
		Toolbar: ['CssClass'],
		Tabs: ['CssClass'],
		Pager: ['Data'],
		Dialog: ['Label', 'Width', 'Height', 'Data'],
		Page: ['Label', 'Data', "CssClass", "UseCollectionView"],
		Button: ['Label', 'CssClass', 'If'],
		GRID_PROPS: ['Row', 'Col', 'RowSpan', 'ColSpan'],
		COMMAND_PROPS: ['Command', 'Argument', 'Url'],
		OTHER_PROPS: {
			Grid: ['Rows', 'Columns'],
			TextBox: ['Multiline', 'Placeholder'],
			DataGridColumn: ['Fit', 'NoWrap', 'LineClamp'],
			Selector: ['Placeholder', 'ShowClear'],
			ComboBox: ['ItemsSource'],
		}
	};

	var propsheetElem = {
		template: propsheetTemplate,
		props: {
			item: Object,
			host: Object
		},
		computed: {
			hasGrid() {
				return this.item.$parent.$parent.Is === 'Grid';
			},
			hasCommand() {
				return this.item.Is === 'Button';
			},
			itemProps() {
				if (!this.item) return [];
				const type = this.item.Is;
				return this.getProps(PROP_MAP[type], this.item);
			},
			otherProps() {
				if (!this.item) return [];
				const type = this.item.Is;
				return this.getProps(PROP_MAP.OTHER_PROPS[type], this.item.Props || {});
			},
			gridProps() {
				let g = this.item.Grid;
				if (!g) return [];
				return this.getProps(PROP_MAP['GRID_PROPS'], this.item.Grid || {});
			},
			commandProps() {
				let g = this.item.Command;
				if (!g) return [];
				return this.getProps(PROP_MAP['COMMAND_PROPS'], this.item.Command || {});
			}
		},
		methods: {
			setDirty() {
				if (!this.host) return;
				this.host.setDirty();
			},
			getProps(props, item) {
				if (!props) return [];
				let that = this;
				return props.map(p => {
					const r = {
						name: p,
						get value() { return item[p] || ''; },
						set value(v) { Vue.set(item, p, v); that.setDirty(); }
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
	<propsheet v-if="activeTab === 'props'" :item=item :host=host />
</div>
`;

	var taskpad = {
		template: taskpadTemplate$1,
		props: {
			item: Object,
			fields: Array,
			components: Array,
			cont: Object,
			host: Object
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
	<button class="btn btn-tb btn-icon" @click="clickCmd('save')" :disabled="disabled()">
		<i class="ico ico-save-outline" />
	</button>
	<button class="btn btn-tb btn-icon" @click=deleteItem >
		<i class="ico ico-clear" />
	</button>
	<div class="divider" />
	<button class="btn btn-tb btn-icon" @click="clickCmd('reload')">
		<i class="ico ico-reload" />
	</button> 
</div>
`;

	var toolbar = {
		template: toolbarTemplate,
		props: {
			host: Object	
		},
		methods: {
			clickCmd(cmd) {
				if (!this.host) return;
				this.host.exec(cmd);
			},
			deleteItem() {
				this.$parent.deleteItem();
			},
			disabled() {
				if (!this.host) return true;
				return !this.host.isDirty();
			}
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

	const periodPickerTemplate = `
<div class="control-group period-picker" :style=controlStyle >
	<label v-text="item.Label" v-if="item.Label"/>
	<div class="input-group">
		<span v-text="item.Data" class="input text-center"/>
		<span class="caret" />
	</div>
</div>
`;

	var periodPicker = {
		template: periodPickerTemplate,
		extends: control
	};

	var layoutelem = {
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
<td class="fd-datagrid-column" @click.stop.prevent="select" :class="{ selected }"
	:draggable=true @dragstart.stop=dragStart>
	<div v-text="item.Label" class="label" />
	<div v-text="item.Data" class="column" />
</td>
`;

	var dataGridColumn = {
		template: dataGridColumnTemplate,
		extends: layoutelem,
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
	<table>
		<tr>
			<DataGridColumn v-for="(c, ix) in item.Items" :item=c :key=ix :cont=cont />
		</tr>
	</table>
	<div class="fd-grid-handle">▷</div>
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

	const locale = window.$$locale || {};

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
		<span class="a2-pager-title" v-html=pagerText></span>
	</div>`,
		props: {
			item: Object,
			cont: Object	
		},
		computed: {
			pagerText() {
				let elems = locale['$PagerElements'] || 'items';
				let ofStr = locale['$Of'] || 'of';
				return `${elems}: <b>1</b>-<b>20</b> ${ofStr} <b>150</b>`;
			}
		}
	};

	const buttonTemplate$1 = `
<button class="btn btn-tb" @click.stop.prevent="select" :class="{ selected }" :draggable=true
		@dragstart.stop=dragStart >
	<i class="ico" :class=icon />
	<span v-if="item.Label" v-text="item.Label" />	
</button>
`;

	const cmdMap = {
		Edit: 'ico-edit',
		EditSelected: 'ico-edit',
		New: 'ico-add',
		Save: 'ico-save-outline',
		SaveAndClose: 'ico-save-close-outline',
		Apply: 'ico-apply',
		UnApply: 'ico-unapply',
		Create: 'ico-add',
		Delete: 'ico-clear',
		Reload: 'ico-reload',
		Print: 'ico-print',
		DeleteSelected: 'ico-clear',
		Dialog: 'ico-account' // TODO???
	};

	var button$1 = {
		template: buttonTemplate$1,
		extends: layoutelem,
		computed: {
			icon() {
				return cmdMap[this.item.Command.Command] || 'ico-menu';
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
		extends: layoutelem,
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
			case 'ComboBox': return 'ico-chevron-down';
		}
		return '';
	}

	const inputControlTemplate = `
<div class="control-group" :style=controlStyle @click=itemClick :class="{ selected }">
<label v-text="item.Label" v-if="item.Label"/>
	<div class="input-group">
		<span v-text="item.Data" class="input" />
		<a v-if="icon">
			<i class="ico" :class="icon" />
		</a>
	</div>
</div>
`;

	const inputControlSimpleTemplate = `
<div class="control-group" :style=controlStyle >
<label v-text="item.Label" v-if="item.Label" />
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
			icon() { return is2icon(this.item.Is); },
			controlStyle() {
				return undefined;
			},
			selected() {
				return this.cont.isActive(this.item);
			}
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


	const checkBoxTemplate = `
<label class="checkbox" :label="item.Label" :style=controlStyle >
	<input type="checkbox" xcheck="true" checked />
	<span v-text=item.Label />
</label>
`;

	const checkBox = {
		template: checkBoxTemplate,
		extends: control
	};

	const separatorTemplate = `
<div role="separator" class="divider" @click.stop.prevent="select" :class="{ selected }" :draggable=true
		@dragstart.stop=dragStart />
`;

	const separator = {
		template: separatorTemplate,
		extends: layoutelem
	};

	const comboBox = {
		template: inputControlSimpleTemplate,
		extends: control,
		computed: {
			icon() { return is2icon(this.item.Is); }
		}
	};

	const staticBox = {
		template: inputControlSimpleTemplate,
		extends: control,
		computed: {
			icon() { return undefined }
		}
	};

	var inputControls = {
		searchBox,
		checkBox,
		comboBox,
		staticBox,
		separator
	};

	var itemToolbar = {
		template: `<div class="toolbar" @dragover=dragOver @drop=drop >
		<component :is="item.Is" v-for="(item, ix) in item.Items" :item="item" :key="ix" :cont=cont />
		<div v-if="!isPage" class="fd-grid-handle">▷</div>
	</div>`,
		extends: layoutelem,
		props: {
			isPage: Boolean,
		},
		components: {
			'Button': button$1,
			'Aligner': aligner,
			'TextBox': textBox,
			'SearchBox': inputControls.searchBox,
			'Separator': inputControls.separator,
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

	var header = {
		template: '<div class="a2-header" v-text="item.Label" />',
		extends: control
	};

	const tabsTemplate = `
<div class="fd-elem-tabs a2-tab-bar">
	<div class="a2-tab-bar-item active" v-for="(itm, ix) in item.Items" :key=ix>
		<a class="a2-tab-button active">
			<span class="content" v-text="itm.Label" />
		</a>
	</div>
	<div class="fd-grid-handle">▷</div>
</div>
`;

	var tabs = {
		template: tabsTemplate,
		props: {
			item: Object,
			cont: Object
		}
	};

	const gridItem = `
<div class="fd-grid-item" :draggable="true"
	@dragstart.stop=dragStart @dragend=dragEnd
	:style="style" @click.stop.prevent=select :class="{ selected }">
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
			'PeriodPicker': periodPicker,
			'DataGrid': datagrid,
			'CheckBox': inputControls.checkBox,
			'ComboBox': inputControls.comboBox,
			'Static': inputControls.staticBox,
			'Label': label, 
			'Header': header,
			'Pager': pager,
			'Toolbar': itemToolbar,
			'Tabs': tabs
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
					height: this.item.Height || '',
					width: this.item.Width || ''
				};
			},
			isSameSelected() {
				let itmIs = this.item.Is;
				return itmIs == 'Grid';
			},
			selected() {
				if (this.isSameSelected) return;
				return this.cont.isActive(this.item);
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
	<div class="fd-grid-handle">▷</div>
</div>
`;

	var gridElem = {
		name: 'grid',
		extends: layoutelem,
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
		extends: layoutelem
	};

	const buttonTemplate = `
<button @click.stop.prevent="select" class="btn a2-inline" :class="btnClass" :draggable=true
	@dragstart.stop=dragStart v-text="item.Label">
</button>
`;

	var button = {
		template: buttonTemplate,
		extends: layoutelem,
		computed: {
			btnClass() {
				return {
					selected: this.selected,
					'btn-primary': this.item.Props?.Style === 'Primary'
				};
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

	const panelTemplate = `
<div class="fd-panel panel panel-transparent" @click.stop=select :class="{selected}">
	<div class="panel-header">
		<div v-text="item.Label" class="panel-header-slot" />
		<span class="ico panel-collapse-handle" />
	</div>
	<component v-for="(itm, ix) in item.Items" :key="ix" :is="itm.Is"
		:item="itm" :cont=cont />
</div>
`;

	var panel = {
		template: panelTemplate,
		extends: layoutelem,
		props: {
			item: Object,
			cont: Object
		}
	};

	const taskpadTemplate = `
<div class="fd-elem-taskpad"  @click.stop=select :class="{selected}">
	<component v-for="(itm, ix) in item.Items" :key="ix" :is="itm.Is"
		:item="itm" :cont=cont />
</div>
`;

	var frmTaskpad = {
		template: taskpadTemplate,
		extends: layoutelem,
		components: {
			'Panel': panel
		},
		props: {
			item: Object,
			cont: Object
		}
	};

	function dataType2Is(dt) {
		switch (dt) {
			case "reference": return "Selector";
			case "bit": return "CheckBox";
			case "date":
			case "datetime": return "DatePicker";
		}
		return "TextBox";
	}

	function field2component(f) {
		return {
			Data: f.Name,
			Is: dataType2Is(f.DataType)
		};
	}

	const containerTemplate = `
<div class="fd-container" @keyup.self=keyUp tabindex=0 >
	<fd-toolbar :host=host></fd-toolbar>
	<fd-taskpad :item=selectedItem :fields=componentFields :cont=cont :components=components :host=host />
	<div class="fd-main" @click.stop.stop=clickBody>
		<div class=fd-body  @click.stop.stop=clickBody :class="bodyClass" :style="bodyStyle">
			<div v-if="isDialog" class="modal-header">
				<span class="modal-title" v-text="form.Label"/>
				<button tabindex="-1" class="btnclose">✕</button>
			</div>
			<div v-if="isPage" class="fd-tabs-header">
				<div class="fd-tab-title" v-text="form.Label"/>
			</div>
			<div class="fd-content">
				<div v-if="hasToolbar" class="form-toolbar">
					<Toolbar :item="form.Toolbar" :cont=cont class="page-toolbar" :is-page="true"/>
				</div>
				<component v-for="(itm, ix) in form.Items" :key="ix" :is="itm.Is"
					:item="itm" :cont=cont />
				<Taskpad :item="form.Taskpad" :cont=cont v-if="hasTaskpad"/>
			</div>
			<dlg-buttons v-if="isDialog" :elems="form.Buttons" :cont=cont />
		</div>
	</div>
</div>
`;

	Vue.component('Grid', gridElem);

	Vue.component('fd-container', {
		template: containerTemplate,
		components: {
			'fd-toolbar': toolbar,
			'fd-taskpad': taskpad,
			'dlg-buttons': dlgButtons,
			'HLine': lineElem,
			'Taskpad': frmTaskpad,
			'Toolbar': itemToolbar
		},
		props: {
			form: Object,
			fields: Array,
			components: Array,
			host: Object
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
			hasTaskpad() {
				return this.form.Taskpad && this.form.Taskpad.Items.length;
			},
			hasToolbar() {
				return this.form.Toolbar && this.form.Toolbar.Items.length;
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
				return this.form.Is === 'Dialog';
			},
			isPage() {
				return this.form.Is === 'Page';
			},
			componentFields() {
				return this.fields.map(field2component);
			}
		},
		methods: {
			clickBody() {
				this.selectedItem = this.form;	
			},
			setDirty() {
				if (this.host)
					this.host.setDirty();
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
				this.selectedItem.$remove();
				this.selectedItem = this.form;
				this.setDirty();
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

				let sg = this.selectedItem || {};

				if (!sg.Grid) {
					// clone element
					let no = Object.assign({}, this.selectedItem);
					no.Items = [];
					no.Grid = { Row: rc.row, Col: rc.col };	
					rc.grid.Items.push(no);
					this.selectedItem = no;
					this.setDirty();
					return;
				}

				let fg = this.selectedItem.$parent;

				if (fg && fg.Is === 'Grid' && fg !== rc.grid) {
					this.selectedItem.$remove();
					rc.grid.Items.$append(this.selectedItem);
				}
				this.selectedItem.Grid = { Row: rc.row, Col: rc.col, ColSpan: sg.ColSpan, RowSpan: sg.RowSpan };
				this.setDirty();
			}
		},
		mounted() {
			this.selectedItem = this.form;
			console.dir(this.fields);
		}
	});

}));
//# sourceMappingURL=formdesigner.js.map

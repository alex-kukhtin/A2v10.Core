(() => {
	const popup = require('std:popup');
	const du = require('std:utils').date;
	const eventBus = require('std:eventBus');
	const loc = require('app:locale');

	const timeBoardTemplate = `
<div class="time-board">
<table>
	<thead><tr>
		<th class="bt-2 bl-2 bb-2" rowspan=3>№</th>
		<th class="bt-2" colspan=2 v-text="localize('@Employee')"></th>
		<th class="bt-2" colspan=2 v-text="localize('@Schedule')"></th>
		<th class="bt-2" :colspan="calLength">Робочий час</th>
		<th class="bt-2 bl-2" :colspan="presences.length + 1">Відпрацьовано</th>
		<th class="bt-2" :colspan="absences.length">Неявки (дні/години)</th>
	</tr>
	<tr>
		<th colspan=2>П.І.Б</th>
		<th rowspan=2>Дні</th>
		<th rowspan=2>Години</th>
		<th v-for="d in topDays" v-text="d" class="text-center"></th>
		<th class="bl-2 bb-2" rowspan=2>Дні</th>
		<th :colspan="presences.length">Години</th>
		<th rowspan=2 class="text-center bb-2" v-for="a in absences" v-text="a.Code" :title="a.Name"></th>
	</tr>
	<tr>
		<th class="bb-2">Таб.№</th>
		<th class="bb-2">Посада</th>
		<th class="text-center bb-2" v-for="d in bottomDays" v-text="d"></th>
		<th class="text-center bb-2" v-for="a in presences" v-text="a.Code" :title="a.Name"></th>
	</tr>
	</thead>
	<tbody>
		<template v-for="(row, rx) in rows">
			<tr>
				<td rowspan=2 class="bb-2 bl-2" v-text="row.RowNo"></td>
				<td colspan=2 v-text="row.Person.Name"></td>
				<td colspan=2 v-text="row.Schedule.Name"></td>
				<td class="text-center code-ed" v-for="td in topDays" :class="cellClass(row, td)" 
					v-text="cellText(row, td)" @click.stop.prevent="click($event, row, td)"></td>
				<td rowspan=2 class="bl-2 bb-2 text-right" v-text="totalDays(row, mainCode)"></td>
				<td rowspan=2 class="text-right bb-2 dis" v-for="a in presences" v-text="totalHours(row, a.Id)"></td>
				<td v-for="a in absences" class="text-right" :class="'code-' + a.Color" v-text="totalDays(row, a.Id)"></td>
			</tr>
			<tr>
				<td class=bb-2 v-text="row.TabNo"></td>
				<td class=bb-2 v-text="row.Position.Name"></td>
				<td class="bb-2 text-right" v-text="row.Schedule.Days"></td>
				<td class="bb-2 text-right" v-text="row.Schedule.Hours"></td>
				<td class="bb-2 text-center code-ed" v-for="bd in bottomDays" :class="cellClass(row, bd)" 
					v-text="cellText(row, bd)" @click.stop.prevent="click($event, row, bd)"></td>
				<td v-for="a in absences" class="text-right bb-2" :class="'code-' + a.Color" v-text="totalHours(row, a.Id)"></td>
			</tr>
		</template>
	</tbody>
	<tfoot>
		<tr>
			<th :colspan="calLength + 5" rowspan=2 class="bl-2 bt-1 bb-2">Всього</th>
			<th rowspan=2 class="bl-2 bt-1 bb-2 text-right" v-text=grandTotalDays(mainCode)></th>
			<th class="text-right bt-1 bb-2" v-for="a in presences" rowspan=2 v-text="grandTotalHours(a.Id)"></th>
			<th v-for="a in absences" class="text-right bt-1" v-text="grandTotalDays(a.Id)"></th>
		</tr>
		<tr>
			<th v-for="a in absences" class="text-right bb-2" v-text=grandTotalHours(a.Id)></th>
		</tr>
	</tfoot>
</table>
<ul class="menu timeboard-select" ref=menu v-show=menuOpen dropdown-top>
	<li class="wk-bar">
		<div class="e-info">
			<span><i class="ico ico-user-role"></i> <span v-text=currentEmployee></span></span>
			<span><i class="ico ico-calendar"></i> <span v-text=currentDate></span></span>
		</div>
		<div class="wd">
			<div v-for="c in presences" class="no-wrap">
				<label v-text=c.Code :title="c.Name"></label>
				<input :value="workHours(c.Id)" @change="setWorkHours($event, c.Id)">
			</div>
		</div>
	</li>
	<li class="sel" v-for="c in absences" @click.stop.prevent="setDay(c.Id)">
		<span class="code" :class="'code-' + c.Color" v-text=c.Code></span> <span v-text="c.Name"></span>
	</li>
	<li class="clear"><span @click.stop.prevent="clearDay()">Очистити</span></li>
</ul></div>
`;

	const range = (min, max) =>
		Array.apply(null, Array(max - min + 1)).map((i, j) => '' + (j + min));

	const createCodeMap = (codes) =>
		Object.assign({}, ...codes.map(x => ({ [x.Id]: x })));

	const findMainCode = (codes) => codes.find(x => x.WorkTime === 'W').Id;

	const weekdayClass = (date, day) => {
		let x = new Date(date.getFullYear(), date.getMonth(), day, 0, 0, 0, 0).getDay();
		return x === 0 ? 'wkday-sun' : x === 6 ? 'wkday-sat' : '';
	};

	const int2time = (val) => {
		if (!val) return '';
		let h = Math.floor(val / 60), m = val % 60;
		if (!m) return '' + h;
		if (m < 10) m = '0' + m;
		return (h || m) ? `${h}:${m}` : '';
	};

	const time2int = (val) => {
		let v = (val || '').split(':');
		let h = v[0], m = 0;
		if (v.length > 1)
			m = v[1];
		return +h * 60 + (+m);
	};

	Vue.component('a2-timeboard', {
		template: timeBoardTemplate,
		props: {
			rows: Array,
			period: Date,
			codes: Array
		},
		data() {
			return {
				codeMap: createCodeMap(this.codes),
				currentRow: Object,
				mainCode: findMainCode(this.codes),
				currentDay: '',
				menuOpen: false
			};
		},
		computed: {
			dayOfMonth() {
				return new Date(this.period.getFullYear(), this.period.getMonth() + 1, 0).getDate();
			},
			calLength() {
				return this.dayOfMonth > 30 ? 16 : 15;
			},
			topDays() {
				let r = range(1, 15);
				if (this.dayOfMonth > 30)
					r.push('');
				return r;
			},
			bottomDays() {
				let dm = this.dayOfMonth;
				let r = range(16, dm);
				while (r.length < 15)
					r.push('');
				return r;
			},
			absences() {
				return this.codes.filter(x => x.Type === 2);
			},
			presences() {
				return this.codes.filter(x => x.Type < 2);
			},
			currentDate() {
				if (!this.currentRow || !this.currentDay) return '';
				let d = du.create(this.period.getFullYear(), this.period.getMonth() + 1, +this.currentDay);
				return du.formatDate(d);
			},
			currentEmployee() {
				if (!this.currentRow || !this.currentDay) return '';
				return this.currentRow.Person.Name;
			}
		},
		methods: {
			localize(key) {
				return loc[key];
			},
			cellText(row, day) {
				let dd = row.Days[day];
				if (!dd) return '';
				if (!dd.DayData.length)
					return;
				if (dd.DayData.length == 1) {
					let x = this.codeMap[dd.DayData[0].Code];
					if (x.Type === 2)
						return x.Code;
				}
				return dd.DayData
					.filter(x => x.Code && this.codeMap[x.Code].Type < 2)
					.map(x => `${this.codeMap[x.Code].Code}${int2time(x.Minutes)}`)
					.join('\n');
			},
			codeValue(row, day) {
				let dd = row.Days[day];
				if (!dd) return null;
				if (dd.DayData.length == 1)
					return this.codeMap[dd.DayData[0].Code];
				return null;
			},
			outdate(row, day) {
				return day < row.StartDay || day > row.EndDay;
			},
			cellClass(row, day) {
				if (this.outdate(row, day))
					return 'no-data';
				let cls = [weekdayClass(this.period, day)];
				let dd = row.Days[day];
				if (row === this.currentRow && day === this.currentDay)
					cls.push('active');
				if (dd) {
					if (dd.DayData.length == 1) {
						let codeVal = this.codeMap[dd.DayData[0].Code];
						if (codeVal && codeVal.Color)
							cls.push(`code-${codeVal.Color}`);
					}
				} else
					cls.push('no-data');
				return cls.join(' ');
			},
			click(event, row, day) {
				eventBus.$emit('closeAllPopups');
				if (this.outdate(row, day))
					return;
				let rd = row.Days[day];
				if (!rd) return;
				this.currentRow = row;
				this.currentDay = day;
				let menu = this.$refs.menu;
				let td = event.srcElement;
				menu.style.top = (td.offsetTop + td.offsetHeight + 1) + 'px';
				menu.style.left = td.offsetLeft + 'px';
				var ae = document.activeElement;
				if (ae && ae.blur) ae.blur();
				this.menuOpen = true;
			},
			totalDays(row, code) {
				let x = range(1, 31).reduce(
					(p, c) => p + row.Days[c].DayData.filter(x => x.Code === code).length,
					0);
				return x ? x : '';
			},
			totalHours(row, code) {
				return int2time(this.totalHoursInt(row, code));
			},
			totalHoursInt(row, code) {
				return range(1, 31).reduce(
					(p, c) => p + row.Days[c].DayData.filter(dd => dd.Code === code)
						.reduce((pd, cd) => pd + cd.Minutes, 0),
					0);
			},
			grandTotalDays(code) {
				let gt = this.rows.reduce((p, c) => p + +(this.totalDays(c, code)), 0);
				return gt ? gt : ''
			},
			grandTotalHours(code) {
				let gt = this.rows.reduce((p, c) => p + +(this.totalHoursInt(c, code)), 0);
				return gt ? int2time(gt) : ''
			},
			setDay(code) {
				if (!this.currentRow || !this.currentDay) return;
				let rd = this.currentRow.Days[this.currentDay];
				if (!rd) return;
				rd.DayData.$empty();
				let sch = this.currentRow.Schedule;
				let mins = +sch.DaysMinute.split(':')[this.currentDay];
				let dd = rd.DayData.$append({ Code: code, Minutes: mins, Day: this.currentDay });
			},
			clearDay() {
				if (!this.currentRow || !this.currentDay) return;
				let rd = this.currentRow.Days[this.currentDay];
				rd.DayData.$empty();
			},
			workHours(code) {
				if (!this.currentRow || !this.currentDay) return '';
				let rd = this.currentRow.Days[this.currentDay];
				if (!rd) return '';
				let dd = rd.DayData.find(x => x.Code === code);
				return dd ? int2time(dd.Minutes) : '';
			},
			setWorkHours($event, code) {
				if (!this.currentRow || !this.currentDay) return;
				let rd = this.currentRow.Days[this.currentDay];
				if (!rd) return;
				let value = '' + $event.srcElement.value;
				let codeobj = this.codeMap[code];
				let maincodeobj = null;
				let mainwh = null;
				if (!codeobj.Main) {
					maincodeobj = this.codeMap[this.mainCode];
					mainwh = rd.DayData.find(x => x.Code == this.mainCode);
					if (!mainwh)
						mainwh = rd.DayData.$append({ Code: maincodeobj.Id, Day: this.currentDay });
				}
				let dd = rd.DayData.find(x => x.Code === code);
				if (!dd)
					dd = rd.DayData.$append({ Code: code, Day: this.currentDay });
				rd.DayData.forEach(d => {
					let c = this.codeMap[d.Code];
					if (c.Type === 2)
						d.$remove();
				});
				if (value) {
					dd.Minutes = time2int(value);
					if (mainwh) {
						let total = rd.DayData.reduce((p, dd) => p + (this.codeMap[dd.Code].Type === 1 ? dd.Minutes : 0), 0);
						if (mainwh.Minutes < total)
							mainwh.Minutes = total;
					}
				}
				else
					dd.$remove();
			},
			__clickOutside(el) {
				this.menuOpen = false;
				this.currentDay = '';
				this.currentRow = null;
			}

		},
		mounted() {
			let menu = this.$refs.menu;
			popup.registerPopup(menu);
			menu._close = this.__clickOutside;
		},
		beforeDestroy() {
			popup.unregisterPopup(this.$refs.menu);
		}
	});
})();

app.modules["app:folders"] = {
	handleSavedFolder,
	deleteFolderCommand,
	moveToGroupCommand
}

async function handleSavedFolder(elem) {
	let ctrl = this.$ctrl;
	let found = this.Folders.$find(f => f.Id === elem.Id);
	if (found) {
		found.Name = elem.Name;
		return;
	}

	let parentId = elem.Folder.Id;
	let elemToAppend = { Icon: 'folder-outline', Id: elem.Id, Name: elem.Name };

	if (!parentId) {
		// top level folder
		let woGroups = this.Folders.$find(f => f.Id === -2); // without groups
		if (woGroups)
			this.Folders.$insert(elemToAppend, "above", woGroups).$select(this.Folders);
		else
			this.Folders.$append(elemToAppend).$select(this.Folders);
		return;
	}
	let foundFolder = this.Folders.$find(f => f.Id === parentId);
	if (!foundFolder)
		return;

	foundFolder.HasSubItems = true;

	if (foundFolder.$expanded)
		foundFolder.SubItems.$append(elemToAppend).$select(this.Folders);
	else {
		await ctrl.$expand(foundFolder, 'SubItems', true);
		let nf = this.Folders.$find(f => f.Id === elem.Id);
		if (nf)
			nf.$select(this.Folders);
	}
}

function deleteFolderCommand(chProp) {
	function canDelete(fld) {
		if (!fld) return false;
		if (fld.HasSubItems) return false;
		if (fld.SubItems.length) return false;
		if (fld[chProp].length) return false;
		if (fld.Id < 0) return false;
		return true;
	}
	async function deleteFolder() {
		const ctrl = this.$ctrl;
		let sel = this.Folders.$selected;
		if (!canDelete(sel)) return false;
		await ctrl.$invoke('deleteFolder', { Id: sel.Id });
		sel.$remove();
	}

	function canDeleteFolder() {
		let sel = this.Folders.$selected;
		return canDelete(sel);
	}

	return {
		exec: deleteFolder,
		canExec: canDeleteFolder,
		confirm: '@[Confirm.Delete.Folder]'
	}
}

function moveToGroupCommand(chProp, path) {

	function canMoveToGroup() { 
		return !!getSelecteElems(this);
	}

	function getSelecteElems(root) {
		let sel = root.Folders.$selected;
		if (!sel) return null;
		let x = sel[chProp].$checked;
		if (!x || !x.length) return null;
		return x;
	}

	async function moveToGroup() {
		let items = getSelecteElems(this);
		if (!items) return;
		let ctrl = this.$ctrl;

		// before browse!!! can be changed
		let selFolder = this.Folders.$selected;

		let folder = await ctrl.$showDialog(`${path}/browsefolder`);
		if (folder.Id === selFolder.Id) {
			ctrl.$alert('@[Error.Element.AlreadyInFolder]');
			return;
		}

		await ctrl.$invoke('moveToFolder', { Folder: folder.Id, Items: items.map(itm => itm.Id).join(',') });

		const resetChildren = (id) => {
			// finds folder in the main tree and resets children's
			let targetFolder = this.Folders.$find(f => f.Id === folder.Id);
			if (targetFolder)
				targetFolder[chProp].$resetLazy();
		};

		resetChildren(folder.Id);
		resetChildren(-2);

		items.forEach(itm => itm.$checked = false);

		if (selFolder.Id == -1) return;

		for (let i = 0; i < items.length; i++)
			items[i].$remove();

		ctrl.$modalClose(true);
	}

	return {
		exec: moveToGroup,
		canExec: canMoveToGroup
	};
}
(() => {

	app.modules["app:stockdoc"] = {
		reloadRems,
		itemRoleChange,
		articleChange,
		setRowPrice
	};

	const utils = require("std:utils");
	const cu = utils.currency;

	async function reloadRems(doc, rows, selfcost) {
		const ctrl = this.$ctrl;
		let rarr = rows.split(',');
		let items = []
		for (let rowName of rarr)
			items.push(...doc[rowName].map(r => r.Item.Id));

		let cmd = selfcost ? 'getSelfCostPricesAndRems' : 'getPricesAndRems';
		let arg = { Items: items.join(','), Date: doc.Date, Wh: doc.WhFrom.Id || undefined };
		if (!selfcost)
			arg.PriceKind = doc.PriceKind.Id;

		let result = await ctrl.$invoke(cmd, arg, '/document/commands');

		// selfcost - use roles, otherwise - items only
		for (let rowName of rarr)
			doc[rowName].forEach(row => {
				let price = selfcost ? result.Prices.find(p => p.Item === row.Item.Id && p.Role === row.ItemRole.Id)
					: result.Prices.find(p => p.Item === row.Item.Id);
				setRowPrice.call(this, row, price ? price.Price : 0);
				let rem = result.Rems.find(p => p.Item === row.Item.Id && p.Role === row.ItemRole.Id);
				row.Rem = rem ? rem.Rem : 0;
			});
	}

	async function itemRoleChange(row, selfcost) {
		const ctrl = this.$ctrl;
		let doc = this.Document;
		let cmd = selfcost ? 'getSelfCostPricesAndRems' : 'getPricesAndRems';
		let arg = { Items: '' + row.Item.Id, Date: doc.Date, Wh: doc.WhFrom.Id || undefined };
		if (!selfcost)
			arg.PriceKind = doc.PriceKind.Id;

		let result = await ctrl.$invoke(cmd, arg, '/document/commands');
		let price = result.Prices.find(p => p.Item === row.Item.Id && p.Role === row.ItemRole.Id);
		row.Price = price ? price.Price : 0;
		let rem = result.Rems.find(p => p.Item === row.Item.Id && p.Role === row.ItemRole.Id);
		row.Rem = rem ? rem.Rem : 0;
	}

	async function articleChange(item, val, withRem) {
		if (!val) {
			item.$empty();
			return;
		};
		const ctrl = this.$ctrl;
		let arg = {
			Text: val.trim(),
			PriceKind: this.Document.PriceKind.Id || undefined,
			Date: this.Document.Date
		};
		if (withRem)
			arg.Wh = this.Document.WhFrom.Id;
		let result = await ctrl.$invoke('findArticle', arg, '/catalog/item');
		result?.Item ? item.$merge(result.Item) : item.$empty();
	}

	function setRowPrice(row, price) {
		let doc = this.Document;
		var priceWithVat = doc.PriceKind.Vat;
		// row.Price always without VAT!
		if (priceWithVat) {
			let vatValue = row.VatRate.Value;
			price = cu.round(price * 100.0 / (100.0 + vatValue), 6);
		}
		row.Price = price;
	}

})();

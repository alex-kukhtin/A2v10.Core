// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

(function () {

	const popup = require('std:popup');
	const utils = require('std:utils');
	const locale = window.$$locale;

	const DEFAULT_DELAY = 300;

	Vue.component('a2-multiselect', {
		template: `
<div class="a2-multiselect control-group" :style="{'--x-width': maxWidth}">
	<label v-text="label" />
	<div class="input-group" @click.stop.prevent=toggle>
		<div class="ms-placeholder" v-if="isEmpty">
			<span v-text="placeholder" />
		</div>
		<div class="ms-text" v-else>
			<span v-for="el in selArray" class="ms-tag">
				<span v-text="el.Name" class="ms-tag-body"/>
				<button @click.stop="removeItem(el)" class="ms-tag-remove">✕</button>
			</span>
		</div>
		<a href="" class="a2-hyperlink add-on" @click.stop.prevent=browseItem>
			<i class="ico ico-search"/>
		</a>
	</div>
	<div v-if="isOpen" class="ms-pane">
		<div class="input-group">
			<input v-focus v-model="query" @input="debouncedUpdate" class="ms-fetch"
				:placeholder="locale.$Search">
		</div>
		<ul class="ms-pane-list">
			<li v-for="itm in items" class="ms-pane-item" @click.stop="clickItem(itm)" :class="itemClass(itm)">
				<i class="ico" :class="itmIcon(itm)" />
				<span v-text="itm.Name" class="ms-pane-item-text line-clamp"/>
			</li>
		</ul>
		<div class="ms-pane-footer">
			<button class="btn btn-primary sm" @click=apply v-text="locale.$Apply"/>
			<button class="btn sm" @click=close v-text="locale.$Cancel"/>
		</div>
	</div>
</div>
`,
		props: {
			item: Object,
			prop: String,
			url: String,
			label: String,
			placeholder: String
		},
		data() {
			return {
				isOpen: false,
				query: '',
				items: [],
				maxWidth: '250px'
			}
		},
		computed: {
			selArray() {
				return this.item[this.prop];
			},
			locale() {
				return locale;
			},
			isEmpty() {
				return !this.selArray.length;
			},
			debouncedUpdate() {
				let delay = DEFAULT_DELAY;
				return utils.debounce(async () => {
					if (!this.query) {
						this.items.splice(0);
						return;
					}
					let el = await this.$root.$invoke('fetch', { Text: this.query }, this.url);
					if (!el) return;
					let keys = Object.keys(el);
					if (!keys || !keys.length) return;
					let arr = el[keys[0]];
					this.items.splice(0);
					for (let ai of arr)
						this.items.push({ Id: ai.Id, Name: ai.Name, Checked: this.isChecked(ai) });
				}, delay);
			},
		},
		methods: {
			async browseItem() {
				let vm = this.$root;
				let be = await vm.$showDialog(this.url + '/browse');
				if (!be) return;
				if (this.selArray.find(a => a.Id === be.Id))
					return;
				let arr = this.item[this.prop];
				let nelem = { Id: be.Id, Name: be.Name };
				if (arr.$append)
					arr.$append(nelem)
				else
					arr.push(nelem);
			},
			isChecked(itm) {
				return !!this.selArray.find(x => x.Id === itm.Id);
			},
			itmIcon(itm) {
				return itm.Checked ? 'ico-checkbox-checked' : 'ico-checkbox';
			},
			clickItem(itm) {
				itm.Checked = !itm.Checked;
			},
			apply() {
				for (let x of this.items) {
					if (x.Checked)
						this.addItemToFilter(x);
					else
						this.removeItemFromFilter(x);
				}
				this.close();
			},
			addItemToFilter(itm) {
				let ix = this.selArray.findIndex(x => x.Id === itm.Id);
				if (ix >= 0) return;
				let nelem = { Id: itm.Id, Name: itm.Name };
				let arr = this.selArray;
				if (arr.$append)
					arr.$append(nelem);
				else
					arr.push(nelem);
			},
			removeItemFromFilter(itm) {
				let arr = this.selArray;
				let ix = arr.findIndex(x => x.Id === itm.Id);
				if (ix < 0) return;
				let el = arr[ix];
				if (el.$remove)
					el.$remove();
				else
					arr.splice(ix, 1);
			},
			itemClass(itm) {
				return itm.Checked ? 'active' : undefined;
			},
			toggle() {
				if (this.isOpen)
					this.close();
				else {
					this.isOpen = true;
					Vue.nextTick(() => {
						let cs = this.$el.getElementsByClassName('ms-fetch');
						if (cs && cs.length && cs[0].focus)
							cs[0].focus();
					});
				}
			},
			close() {
				this.isOpen = false;
				this.items.splice(0);
				this.query = '';
			},
			removeItem(x) {
				if (x.$remove)
					x.$remove();
				else {
					let ix = this.selArray.indexOf(x);
					if (ix < 0) return;
					this.selArray.splice(ix, 1);
				}
			},
			__clickOutside(el) {
				if (el && el.closest && el.closest(".ms-pane"))
					return;
				this.close();
			}
		},
		mounted() {
			popup.registerPopup(this.$el);
			this.$el._close = this.__clickOutside;
			this.maxWidth = (this.$el.offsetWidth - 62) + 'px';
		},
		destroyed() {
			popup.unregisterPopup(this.$el);
		}
	});
})();

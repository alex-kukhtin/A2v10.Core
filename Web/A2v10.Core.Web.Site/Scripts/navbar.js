// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

/*20240807-8333*/
/* tabbed:navbar.js */
(function () {

	const popup = require('std:popup');
	const eventBus = require('std:eventBus');

	Vue.component("a2-mdi-navbar", {
		template: `
<div class="mdi-navbar">
	<ul class="bar">
		<li v-for="m in menu" @click.stop.prevent=clickMenu(m) :title=m.Name :class="menuClass(m)">
			<i class="ico" :class="menuIcon(m)"></i>
			<span v-text="m.Name" class="menu-text"></span>
		</li>
	</ul>
	<div class="mdi-menu" v-if="isMenuVisible">
		<div class="menu-title" v-text=activeMenu.Name></div>
		<ul>
			<li v-for="m in activeMenu.Menu" class="level-0">
				<span class="folder" v-text="m.Name"></span>
				<ul v-if="!!m.Menu">
					<li v-for="im in m.Menu" class="level-1" @click.stop.prevent="clickSubMenu(im.Url, im.Name)">
						<span v-text="im.Name"></span>
						<button v-if="im.CreateUrl" class="btn-plus ico ico-plus-circle" 
							:title="im.CreateName" @click.stop.prevent="clickSubMenu(im.CreateUrl, im.CreateName)"></button>
					</li>
				</ul>
			</li>
		</ul>
	</div>
</div>
	`,
		props: {
			menu: Array,
		},
		data() {
			return {
				activeMenu: null
			};
		},
		computed: {
			isMenuVisible() {
				return !!this.activeMenu;
			}
		},
		methods: {
			clickMenu(m) {
				let am = this.activeMenu;
				eventBus.$emit('closeAllPopups');
				const shell = this.$parent;
				if (m.ClassName === 'grow')
					return;
				if (!m.Menu) {
					this.activeMenu = null;
					shell.$emit('navigate', { title: m.Name, url: m.Url });
				} else if (am === m) {
					this.activeMenu = null;
				} else {
					this.activeMenu = m;
				}
			},
			clickSubMenu(url, title) {
				eventBus.$emit('closeAllPopups');
				const shell = this.$parent;
				this.activeMenu = null;
				if (url.startsWith("page:"))
					shell.$emit('navigate', { title: title, url: url.substring(5) });
				else if (url.startsWith("dialog:")) {
					const dlgData = { promise: null, rd: true, direct: true };
					eventBus.$emit('modal', url.substring(7), dlgData);
				} else
					alert('invalid menu url: ' + url);
			},
			menuClass(m) {
				return (m.ClassName ? m.ClassName : '') + ((m === this.activeMenu) ? ' active' : '');
			},
			menuIcon(m) {
				return 'ico-' + m.Icon;
			},
			__clickOutside() {
				this.activeMenu = null;
			}
		},
		mounted() {
			popup.registerPopup(this.$el);
			this.$el._close = this.__clickOutside;
		}
	});
})();

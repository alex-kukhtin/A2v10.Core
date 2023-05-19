(function () {

	const popup = require('std:popup');
	const eventBus = require('std:eventBus');

	Vue.component("a2-mdi-navbar", {
		template: `
<div class="mdi-navbar">
	<ul class="bar">
		<li v-for="m in menu" @click.stop.prevent=clickMenu(m) :title=m.Name :class="m.ClassName"">
			<i class="ico" :class="menuIcon(m)"></i>
		</li>
	</ul>
	<div class="mdi-menu" v-if="isMenuVisible">
		<ul>
			<li v-for="m in activeMenu.Menu" class="level-0">
				<span class="folder" v-text="m.Name"></span>
				<ul v-if="!!m.Menu">
					<li v-for="im in m.Menu" class="level-1" @click.stop.prevent="clickSubMenu(im)" v-text="im.Name">
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
				activeMenu: null,
				popupVisible: false
			};
		},
		computed: {
			isMenuVisible() {
				return !!this.activeMenu && this.popupVisible;
			}
		},
		methods: {
			clickMenu(m) {
				eventBus.$emit('closeAllPopups');
				const shell = this.$parent;
				if (m.ClassName === 'grow')
					return;
				if (!m.Menu) {
					this.popupVisible = false;
					shell.$emit('navigate', { title: m.Name, url: m.Url });
				} else if (this.activeMenu === m && this.popupVisible) {
					this.popupVisible = false;
				} else {
					this.popupVisible = true;
					this.activeMenu = m;
				}
			},
			clickSubMenu(m1) {
				eventBus.$emit('closeAllPopups');
				const shell = this.$parent;
				this.popupVisible = false;
				shell.$emit('navigate', { title: m1.Name, url: m1.Url });
			},
			menuIcon(m) {
				return 'ico-' + m.Icon;
			},
			__clickOutside() {
				this.popupVisible = false;
			}
		},
		mounted() {
			popup.registerPopup(this.$el);
			this.$el._close = this.__clickOutside;
		}
	});
})();

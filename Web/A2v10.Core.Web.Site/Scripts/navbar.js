

Vue.component("a2-mdi-navbar", {
	template: `
<div class="mdi-navbar">
	<ul>
	  <li v-for="m in menu">
		<a v-text=m.Name href="" @click.stop.prevent=clickMenu(m)></a>
	  </li>
	</ul>
	<div class="mdi-menu" v-if=isMenuVisible>
		<ul>
			<li v-for="m in activeMenu.Menu">
				<span v-text="m.Name"></span>
				<ul v-if="!!m.Menu">
					<li v-for="im in m.Menu">
						<a v-text="im.Name" @click.stop.prevent="clickSubMenu(im)"></a>
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
	computed:{
		isMenuVisible() {
			return !!this.activeMenu;
		}
	},
	methods: {
		clickMenu(m) {
			const shell = this.$parent;
			if (!m.Menu)
				shell.$emit('navigate', { title: m.Name, url: m.Url });
			else
				this.activeMenu = m;
		},
		clickSubMenu(m1) {
			const shell = this.$parent;
			shell.$emit('navigate', { title: m1.Name, url: `${this.activeMenu.Url}/${m1.Url}` });
		}
	},
	mounted() {
		console.dir('navbar mounted');
		console.dir(this.menu)
	}
});

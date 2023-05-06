

Vue.component("a2-mdi-header", {
	template: `
<div class="mdi-header">
<span v-text=title></span>
</div>
	`,
	props: {
		title: String,
		subTitle: String
	},
	mounted() {
		console.dir('header mounted');
		console.dir(this.title);
	}
});



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


(function () {
	const eventBus = require('std:eventBus');
	const popup = require('std:popup');
	const utils = require('std:utils');
	const urlTools = require('std:url');

	const modalComponent = component('std:modal');
	const toastr = component('std:toastr');

	app.components["std:shellPlain"] = Vue.extend({
		data() {
			return {
				tabs: [],
				activeTab: null,
				modals: [],
				modalRequeryUrl: ''
			};
		},
		components: {
			"a2-modal": modalComponent
		},
		computed: {
			hasModals() { return this.modals.length > 0; },
		},
		methods: {
			navigate(m) {
				let tab = this.tabs.find(tab => tab.url == m.url);
				if (!tab) {
					tab = { title: m.title, url: m.url, source: `${m.url}/index` };
					this.tabs.push(tab);
				}
				this.activeTab = tab;
			},
			isTabActive(tab) {
				return tab === this.activeTab;
			},
			selectTab(tab) {
				this.activeTab = tab;
			},
			closeTab(tab) {
				let tabIndex = this.tabs.indexOf(tab);
				if (tabIndex == -1)
					return;
				if (tabIndex > 0)
					this.activeTab = this.tabs[tabIndex - 1];
				else if (this.tabs.length > 1)
					this.activeTab = this.tabs[tabIndex + 1];
				else
					this.activeTab = null;
				this.tabs.splice(tabIndex, 1);
			},
			showModal(modal, prms) {
				let id = utils.getStringId(prms ? prms.data : null);
				let raw = prms && prms.raw;
				let root = window.$$rootUrl;
				let url = urlTools.combine(root, '/_dialog', modal, id);
				if (raw)
					url = urlTools.combine(root, modal, id);
				//url = store.replaceUrlQuery(url, prms.query);
				let dlg = { title: "dialog", url: url, prms: prms.data, wrap: false, rd: prms.rd };
				dlg.promise = new Promise(function (resolve, reject) {
					dlg.resolve = resolve;
				});
				prms.promise = dlg.promise;
				this.modals.push(dlg);
				this.setupWrapper(dlg);
			},
			setupWrapper(dlg) {
				this.modalRequeryUrl = '';
				setTimeout(() => {
					dlg.wrap = true;
					//console.dir("wrap:" + dlg.wrap);
				}, 50); // same as modal
			},
			modalCreated(instance) {
				const findRealDialog = () => {
					// skip alerts, confirm, etc
					for (let i = this.modals.length - 1; i >= 0; --i) {
						let md = this.modals[i];
						if (md.rd)
							return md;
					}
					return null;
				}
				// include instance!
				let dlg = findRealDialog();
				if (!dlg) return;
				dlg.instance = instance;
			},
			modalClose(result) {
				if (!this.modals.length) return;

				const dlg = this.modals[this.modals.length - 1];

				const closeImpl = (closeResult) => {
					let dlg = this.modals.pop();
					if (closeResult)
						dlg.resolve(closeResult);
				}

				if (!dlg.attrs) {
					closeImpl(result);
					return;
				}

				if (dlg.attrs.alwaysOk)
					result = true;

				if (dlg.attrs.canClose) {
					let canResult = dlg.attrs.canClose();
					if (canResult === true)
						closeImpl(result);
					else if (canResult.then) {
						result.then(function (innerResult) {
							if (innerResult === true)
								closeImpl(result);
							else if (innerResult) {
								closeImpl(innerResult);
							}
						});
					}
					else if (canResult)
						closeImpl(canResult);
				} else {
					closeImpl(result);
				}
			},
			modalCloseAll() {
				while (this.modals.length) {
					let dlg = this.modals.pop();
					dlg.resolve(false);
				}
			}
		},
		mounted() {
		},
		created() {
			const me = this;
			popup.startService();
			this.$on('navigate', this.navigate);
			eventBus.$on('closeAllPopups', popup.closeAll);
			eventBus.$on('modal', this.showModal);
			eventBus.$on('modalCreated', this.modalCreated);
			eventBus.$on('modalClose', this.modalClose);
			eventBus.$on('modalCloseAll', this.modalCloseAll);
		}
	});
})();
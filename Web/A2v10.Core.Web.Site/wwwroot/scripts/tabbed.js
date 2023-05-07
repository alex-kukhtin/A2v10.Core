

Vue.component("a2-mdi-header", {
	template: `
<div class="mdi-header">
	<span v-text=title></span>
	<div class="aligner"></div>
	<slot></slot>
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


(function () {

	const popup = require('std:popup');

	Vue.component("a2-mdi-navbar", {
		template: `
<div class="mdi-navbar">
	<ul class="bar">
		<li v-for="m in menu" @click.stop.prevent=clickMenu(m) :title=m.Name>
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
				const shell = this.$parent;
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
				const shell = this.$parent;
				this.popupVisible = false;
				shell.$emit('navigate', { title: m1.Name, url: `${this.activeMenu.Url}/${m1.Url}` });
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

(function () {
	const eventBus = require('std:eventBus');
	const popup = require('std:popup');
	const utils = require('std:utils');
	const urlTools = require('std:url');
	const log = require('std:log');

	const modalComponent = component('std:modal');
	const toastr = component('std:toastr');

	app.components["std:shellPlain"] = Vue.extend({
		data() {
			return {
				tabs: [],
				activeTab: null,
				modals: [],
				modalRequeryUrl: '',
				traceEnabled: log.traceEnabled(),
				debugShowTrace: false,
				debugShowModel: false,
				dataCounter: 0,
				sidePaneUrl: '',
			};
		},
		components: {
			"a2-modal": modalComponent
		},
		computed: {
			modelStack() { return this.__dataStack__; },
			hasModals() { return this.modals.length > 0; },
			sidePaneVisible() { return !!this.sidePaneUrl; },
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
				if (tab !== this.activeTab)
					; // do nothing
				else if (tabIndex > 0)
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
			},
			debugTrace() {
				if (!window.$$debug) return;
				this.debugShowModel = false;
				this.debugShowTrace = !this.debugShowTrace;
			},
			debugModel() {
				if (!window.$$debug) return;
				this.debugShowTrace = false;
				this.debugShowModel = !this.debugShowModel;
			},
			debugClose() {
				this.debugShowModel = false;
				this.feedbackVisible = false;
			},
			registerData(component, out) {
				this.dataCounter += 1;
				if (component) {
					if (this.__dataStack__.length > 0)
						out.caller = this.__dataStack__[0];
					this.__dataStack__.unshift(component);
				} else {
					this.__dataStack__.shift(component);
				}
			},
			showSidePane(url) {
				if (!url) {
					this.sidePaneUrl = '';
				} else {
					let newurl = '/_page' + url;
					if (this.sidePaneUrl === newurl)
						this.sidePaneUrl = '';
					else
						this.sidePaneUrl = newurl;
				}
			}
		},
		watch: {
			traceEnabled(val) {
				log.enableTrace(val);
			}
		},
		mounted() {
		},
		created() {
			const me = this;
			me.__dataStack__ = [];
			popup.startService();
			this.$on('navigate', this.navigate);
			eventBus.$on('closeAllPopups', popup.closeAll);
			eventBus.$on('modal', this.showModal);
			eventBus.$on('modalCreated', this.modalCreated);
			eventBus.$on('modalClose', this.modalClose);
			eventBus.$on('modalCloseAll', this.modalCloseAll);
			eventBus.$on('registerData', this.registerData);
			eventBus.$on('showSidePane', this.showSidePane);
		}
	});
})();
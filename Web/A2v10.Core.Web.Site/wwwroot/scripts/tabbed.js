
(function () {

	const locale = window.$$locale;
	const http = require("std:http");
	console.dir(http);



	Vue.component("a2-mdi-header", {
		template: `
	<div class="mdi-header">
		<div class="app-title" v-text=title></div>
		<div class="aligner"></div>
		<slot></slot>
		<div class="dropdown dir-down separate" v-dropdown>
			<button class="user-name" toggle :title="personName"><i class="ico ico-user"></i> 
				<span id="layout-person-name" class="person-name" v-text="personName"></span>
				<span class="caret"></span>
			</button>
			<div class="dropdown-menu menu down-left">
				<a href="" @click.stop.prevent="logout" tabindex="-1" class="dropdown-item">
					<i class="ico ico-logout"></i> 
					<span v-text="locale.$Quit"></span>
				</a>
			</div>
		</div>
	</div>
		`,
		props: {
			title: String,
			subTitle: String,
			personName: String
		},
		computed: {
			locale() { return locale; }
		},
		methods: {
			async logout() {
				await http.post('/account/logout2');
				window.location.assign('/account/login');
			}
		},
		mounted() {
			console.dir('header mounted');
			console.dir(this.title);
		}
	});
})();
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
		<div class="menu-title" v-text=activeMenu.Name></div>
		<ul>
			<li v-for="m in activeMenu.Menu" class="level-0">
				<span class="folder" v-text="m.Name"></span>
				<ul v-if="!!m.Menu">
					<li v-for="im in m.Menu" class="level-1" @click.stop.prevent="clickSubMenu(im.Url, im.Name)">
						<span v-text="im.Name"></span>
						<button v-if="im.CreateUrl" class="btn-plus ico ico-plus-circle" 
							:title="im.CreateTooltip" @click.stop.prevent="clickSubMenu(im.CreateUrl, im.CreateTooltip)"></button>
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
			clickSubMenu(url, title) {
				eventBus.$emit('closeAllPopups');
				const shell = this.$parent;
				this.popupVisible = false;
				if (url.startsWith("page:"))
					shell.$emit('navigate', { title: title, url: url.substring(5) });
				else if (url.startsWith("dialog:")) {
					const dlgData = { promise: null, rd: true, direct: true };
					eventBus.$emit('modal', url.substring(7), dlgData);
				} else
					alert('invalid menu url: ' + url);
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
	const toastrComponent = component('std:toastr');

	let tabKey = 77;

	app.components["std:shellPlain"] = Vue.extend({
		data() {
			return {
				tabs: [],
				closedTabs: [],
				activeTab: null,
				modals: [],
				modalRequeryUrl: '',
				traceEnabled: log.traceEnabled(),
				debugShowTrace: false,
				debugShowModel: false,
				dataCounter: 0,
				sidePaneUrl: '',
				tabPopupOpen: false,
				navigatingUrl: '',
				lockRoute: false,
				requestsCount: 0
			};
		},
		components: {
			'a2-modal': modalComponent,
			'a2-toastr': toastrComponent
		},
		computed: {
			modelStack() { return this.__dataStack__; },
			hasModals() { return this.modals.length > 0; },
			sidePaneVisible() { return !!this.sidePaneUrl; },
			storageKey() { return this.appData.appId + '_tabs'; },
			processing() { return !this.hasModals && this.requestsCount > 0; },
		},
		methods: {
			navigate(m) {
				let tab = this.tabs.find(tab => tab.url == m.url);
				if (!tab) {
					let parentUrl = '';
					if (this.activeTab)
						parentUrl = this.activeTab.url || '';
					tab = { title: m.title, url: m.url, loaded: true, key: tabKey++, root: null, parentUrl: parentUrl };
					this.tabs.push(tab);
					var cti = this.closedTabs.findIndex(t => t.url === m.url);
					if (cti >= 0)
						this.closedTabs.splice(cti, 1);
				}
				tab.loaded = true;
				this.activeTab = tab;
				if (this.tabs.length > 10)
					this.tabs.splice(0, 1);
				this.storeTabs();
			},
			navigateTo(to) {
				this.navigatingUrl = to.url;
				this.navigate({ url: to.url, title: '' });
			},
			setDocTitle(title) {
				let tab = this.activeTab;
				if (!tab && this.navigatingUrl)
					tab = this.tabs.find(tab => tab.url === this.navigatingUrl);
				if (tab)
					tab.title = title;
			},
			setNewId(route) {
				let tab = this.tabs.find(tab => tab.url === route.from);
				if (!tab) return;
				this.lockRoute = true;
				tab.url = route.to;
				if (tab.root)
					tab.root.__tabUrl__ = tab.url;
				Vue.nextTick(() => {
					this.lockRoute = false;
				});
				this.storeTabs();
			},
			tabLoadComplete(page) {
				if (page) {
					let tab = this.tabs.find(t => t.url == page.src);
					if (tab) {
						tab.root = page.root;
						if (page.root) {
							page.root.__tabUrl__ = tab.url;
							page.root.$store.commit('setroute', tab.url);
						}
					}
				}
				this.navigatingUrl = '';
			},
			isTabActive(tab) {
				return tab === this.activeTab;
			},
			isHomeActive() {
				return !this.activeTab;
			},
			tabTitle(tab) {
				let star = '';
				if (tab.root && tab.root.$isDirty)
					star = '* ';
				return star + tab.title;
				
			},
			tabSource(tab) {
				return tab.loaded ? tab.url : null;
			},		
			homeSource() {
				return '/_home/index/0';
			},
			selectHome(noStore) {
				this.activeTab = null;
				if (noStore)
					return;
				this.storeTabs();
			},
			selectTab(tab, noStore) {
				eventBus.$emit('closeAllPopups');
				tab.loaded = true;
				this.activeTab = tab;
				if (noStore)
					return;
				this.storeTabs();
			},
			reopenTab(tab) {
				eventBus.$emit('closeAllPopups');
				this.navigate(tab);
				let ix = this.closedTabs.indexOf(tab);
				this.closedTabs.splice(ix, 1);
				this.storeTabs();
			},
			closeTabFromStore(state) {
				if (!state || !state.root) return;
				let route = state.root.__tabUrl__;
				let tabIndex = this.tabs.findIndex(t => t.url === route);
				if (tabIndex >= 0)
					this.removeTab(tabIndex);
			},
			closeTab(tab) {
				eventBus.$emit('closeAllPopups');
				let tabIndex = this.tabs.indexOf(tab);
				if (tabIndex == -1)
					return;
				if (tab !== this.activeTab)
					; // do nothing
				if (tab.root && tab.root.$close)
					tab.root.$close();
				else
					this.removeTab(tabIndex);
			},
			removeTab(tabIndex) {
				let currentTab = this.tabs[tabIndex];
				let parent = this.tabs.find(t => t.url === currentTab.parentUrl);
				if (parent)
					this.selectTab(parent, true);
				else if (tabIndex > 0)
					this.selectTab(this.tabs[tabIndex - 1], true);
				else if (this.tabs.length > 1)
					this.selectTab(this.tabs[tabIndex + 1], true);
				else
					this.selectHome(true);
				let rt = this.tabs.splice(tabIndex, 1);
				if (rt.length) {
					this.closedTabs.unshift(rt[0]);
					if (this.closedTabs.length > 10)
						this.closedTabs.pop();
				}
				this.storeTabs();
			},
			storeTabs() {
				var mapTab = (t) => { return { title: t.title, url: t.url, parentUrl: t.parentUrl }; };
				let ix = this.tabs.indexOf(this.activeTab);
				let tabs = JSON.stringify({
					index: ix,
					tabs: this.tabs.map(mapTab),
					closedTabs: this.closedTabs.map(mapTab),
				});
				window.localStorage.setItem(this.storageKey, tabs);
			},
			restoreTabs() {
				let tabs = window.localStorage.getItem(this.storageKey);
				if (!tabs)
					return;
				try {
					let elems = JSON.parse(tabs);
					let ix = elems.index;
					for (let i = 0; i < elems.tabs.length; i++) {
						let t = elems.tabs[i];
						let loaded = ix === i;
						if (loaded)
							this.navigatingUrl = t.url;
						this.tabs.push({ title: t.title, url: t.url, loaded, key: tabKey++, root: null, parentUrl: t.parentUrl });
					}
					for (let i = 0; i < elems.closedTabs.length; i++) {
						let t = elems.closedTabs[i];
						this.closedTabs.push({ title: t.title, url: t.url, loaded: true, key: tabKey++ });
					}
					if (ix >= 0 && ix < this.tabs.length)
						this.activeTab = this.tabs[ix];
					else
						this.selectHome(true);
				} catch (err) {
				}
			},
			toggleTabPopup() {
				this.tabPopupOpen = !this.tabPopupOpen;
			},
			showModal(modal, prms) {
				let id = utils.getStringId(prms ? prms.data : null);
				let raw = prms && prms.raw;
				let direct = prms && prms.direct;
				let root = window.$$rootUrl;
				let url = urlTools.combine(root, '/_dialog', modal, id);
				if (raw)
					url = urlTools.combine(root, modal, id);
				else if (direct)
					url = urlTools.combine(root, '/_dialog', modal);
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
			_eventModalClose(result) {
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
			_eventModalCloseAll() {
				while (this.modals.length) {
					let dlg = this.modals.pop();
					dlg.resolve(false);
				}
			},
			_eventConfirm(prms) {
				let dlg = prms.data;
				dlg.wrap = false;
				dlg.promise = new Promise(function (resolve) {
					dlg.resolve = resolve;
				});
				prms.promise = dlg.promise;
				this.modals.push(dlg);
				this.setupWrapper(dlg);
			},
			_eventToParentTab(ev) {
				let tab = this.tabs.find(t => t.root === ev.source);
				if (!tab || !tab.root || !tab.parentUrl) return;
				let ptab = this.tabs.find(t => t.url === tab.parentUrl);
				if (ptab && ptab.root)
					ptab.root.$data.$emit(ev.event, ev.data);
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
				this.debugShowTrace = false;
			},
			registerData(component, out) {
				this.dataCounter += 1;
				if (component) {
					if (this.__dataStack__.length > 0)
						out.caller = this.__dataStack__[0];
					this.__dataStack__.unshift(component);
				}
				else if (this.__dataStack__.length > 1)
					this.__dataStack__.shift();
			},
			updateModelStack(root) {
				if (this.__dataStack__.length > 0 && this.__dataStack__[0] === root)
					return;
				this.__dataStack__.splice(0);
				this.__dataStack__.unshift(root);
				this.dataCounter += 1; // refresh
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
			},
			__clickOutside() {
				this.tabPopupOpen = false;
			}
		},
		watch: {
			traceEnabled(val) {
				log.enableTrace(val);
			},
			activeTab(newtab) {
				if (newtab && newtab.root)
					this.updateModelStack(newtab.root)
			}
		},
		mounted() {
			popup.registerPopup(this.$el);
			// home page here this.tabs.push({})
			this.$el._close = this.__clickOutside;
			this.restoreTabs();
		},
		created() {
			const me = this;
			me.__dataStack__ = [];
			popup.startService();
			this.$on('navigate', this.navigate);
			eventBus.$on('navigateto', this.navigateTo);
			eventBus.$on('setdoctitle', this.setDocTitle);
			eventBus.$on('setnewid', this.setNewId);
			eventBus.$on('closeAllPopups', popup.closeAll);
			eventBus.$on('modal', this.showModal);
			eventBus.$on('modalCreated', this.modalCreated);
			eventBus.$on('modalClose', this._eventModalClose);
			eventBus.$on('modalCloseAll', this._eventModalCloseAll);
			eventBus.$on('registerData', this.registerData);
			eventBus.$on('showSidePane', this.showSidePane);
			eventBus.$on('confirm', this._eventConfirm);
			eventBus.$on('closePlain', this.closeTabFromStore);
			eventBus.$on('toParentTab', this._eventToParentTab);
			eventBus.$on('beginRequest', () => {
				me.requestsCount += 1;
				window.__requestsCount__ = me.requestsCount;
			});
			eventBus.$on('endRequest', () => {
				me.requestsCount -= 1;
				window.__requestsCount__ = me.requestsCount;
			});

		}
	});
})();

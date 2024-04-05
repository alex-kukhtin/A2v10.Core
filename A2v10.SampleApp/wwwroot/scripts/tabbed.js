// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

/*20230605-8109*/
app.modules['std:signalR'] = function () {

	const eventBus = require('std:eventBus')

	const connection = new signalR.HubConnectionBuilder()
		.withUrl("/_userhub")
		.withAutomaticReconnect()
		.configureLogging(signalR.LogLevel.Information)
		.build();

	connection.on('signal', (event, data) => {
		eventBus.$emit('signalEvent', { event, data })
	});

	return {
		startService,
	};

	async function startService() {
		try {
			await connection.start();
			console.log("SignalR Connected.");
		} catch (err) {
			console.log(err);
			setTimeout(start, 5000);
		}
	}
};

// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

/*20230525-8100*/
/* tabbed:appheader.js */
(function () {

	const locale = window.$$locale;
	const http = require("std:http");
	const eventBus = require("std:eventBus");

	Vue.component("a2-mdi-header", {
		template: `
	<div class="mdi-header">
		<div class="app-logo" v-if="hasLogo">
			<img :src="logo"></img>
		</div>
		<div v-else class="app-title" v-text=title></div>
		<div class="aligner"></div>
		<slot></slot>
		<div class="dropdown dir-down separate" v-dropdown>
			<button class="user-name" toggle :title="personName"><i class="ico ico-user"></i> 
				<span id="layout-person-name" class="person-name" v-text="personName"></span>
				<span class="caret"></span>
			</button>
			<div class="dropdown-menu menu down-left">
				<button v-if=hasProfile @click=profile tabindex="-1" class="dropdown-item">
					<i class="ico ico-user"></i> 
					<span v-text="profileText"></span>
				</button>
				<button @click=logout tabindex="-1" class="dropdown-item">
					<i class="ico ico-logout"></i> 
					<span v-text="locale.$Quit"></span>
				</button>
			</div>
		</div>
	</div>
		`,
		props: {
			title: String,
			subTitle: String,
			personName: String,
			hasProfile: Boolean,
			profileText: String,
			logo: String
		},
		computed: {
			locale() { return locale; },
			hasLogo() { return !!this.logo; }
		},
		methods: {
			async logout() {
				await http.post('/account/logout2');
				window.location.assign('/account/login');
			},
			profile() {
				eventBus.$emit('navigateto', { url: '/_profile/index/0'});
			}
		},
		mounted() {
		}
	});
})();
// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

/*20230901-8147*/
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
				if (url.endsWith('{genrandom}')) {
					let randomString = Math.random().toString(36).substring(2);
					url = url.replace('{genrandom}', randomString);
				}
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

// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

/*20240403-8272*/

/* tabbed:shell.js */
(function () {
	const eventBus = require('std:eventBus');
	const popup = require('std:popup');
	const utils = require('std:utils');
	const urlTools = require('std:url');
	const log = require('std:log');
	const signalR = require('std:signalR');

	const modalComponent = component('std:modal');
	const toastrComponent = component('std:toastr');
	const store = component('std:store');

	let tabKey = 77;

	const tabUrlKey = tab => `${tab.url}:${tab.key}`;

	app.components["std:shellPlain"] = Vue.extend({
		store,
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
				homePageTitle: '',
				homeLoaded: false,
				lockRoute: false,
				requestsCount: 0,
				contextTabKey: 0,
				newVersionAvailable: false
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
			storageKey() { return `${this.appData.appId}_${this.appData.userId}_tabs`; },
			processing() { return !this.hasModals && this.requestsCount > 0; },
			canPopupClose() { return this.contextTabKey > 10; /* 10 - home */ },
			canPopupCloseRight() { return this.contextTabKey && this.tabs.some(v => v.key > this.contextTabKey); },
			canReopenClosed() { return this.closedTabs.length > 0 },
			hasModified() { return this.tabs.some(t => t.root && t.root.$isDirty); },
			maxTabWidth() { return `calc((100% - 50px) / ${this.tabs.length})`; }
		},
		methods: {
			navigate(u1) {
				let tab = this.tabs.find(tab => tab.url == u1.url);
				if (!tab) {
					let parentUrl = '';
					if (this.activeTab)
						parentUrl = this.activeTab.url || '';
					tab = { title: u1.title, url: u1.url, query: u1.query || '', loaded: true, key: tabKey++, root: null, parentUrl: parentUrl, reload: 0, debug: false };
					this.tabs.push(tab);
					var cti = this.closedTabs.findIndex(t => t.url === u1.url);
					if (cti >= 0)
						this.closedTabs.splice(cti, 1);
				}
				tab.loaded = true;
				this.activeTab = tab;
				if (this.tabs.length > 10)
					this.tabs.splice(0, 1);
				this.storeTabs();
			},
			navigateUrl(url) {
				this.navigatingUrl = url;
				this.navigate({ url: url, title: '' });
			},
			navigateTo(to) {
				this.navigatingUrl = to.url;
				this.navigate({ url: to.url, title: '' });
			},
			reloadApplication() {
				window.location.reload();
			},
			setDocTitle(title) {
				let tab = this.activeTab;
				if (!tab && this.navigatingUrl)
					tab = this.tabs.find(tab => tab.url === this.navigatingUrl);
				if (tab) {
					tab.title = title;
					document.title = title;
				} else {
					this.homePageTitle = title;
					document.title = title;
				}
			},
			setNewId(route) {
				let tab = this.tabs.find(tab => tab.url === route.from);
				if (!tab) return;
				this.lockRoute = true;
				tab.url = route.to;
				if (tab.root)
					tab.root.__tabUrl__ = tabUrlKey(tab);
				Vue.nextTick(() => {
					this.lockRoute = false;
				});
				this.storeTabs();
			},
			tabLoadComplete(page) {
				if (page) {
					let tab = this.activeTab || this.tabs.find(t => t.url == page.src);
					if (tab) {
						tab.root = page.root;
						document.title = tab.title;
						if (page.root) {
							page.root.__tabUrl__ = tabUrlKey(tab);
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
			fitText(t) {
				if (!t) return 'untitled';
				return t.length > 30 ? t.substring(0, 30) + '…' : t;
			},
			tabTitle(tab) {
				let star = '';
				if (tab.root && tab.root.$isDirty)
					star = '* ';
				return star + tab.title;
				
			},
			tabSource(tab) {
				return tab.loaded ? (tab.url  + (tab.query || '')) : null;
			},		
			homeSource() {
				return this.homeLoaded ? '/_home/index/0' : null;
			},
			selectHome(noStore) {
				this.homeLoaded = true;
				this.activeTab = null;
				document.title = this.homePageTitle;
				if (noStore)
					return;
				this.storeTabs();
			},
			selectTab(tab, noStore, ev) {
				eventBus.$emit('closeAllPopups');
				if (ev && ev.ctrlKey) {
					tab.debug = true;
					return;
				}
				tab.loaded = true;
				this.activeTab = tab;
				document.title = tab.title;
				if (noStore)
					return;
				this.storeTabs();
			},
			reopenTab(tab) {
				eventBus.$emit('closeAllPopups');
				this.navigate(tab);
			},
			closeTabFromStore(state) {
				if (!state || !state.root) return;
				let route = state.root.__tabUrl__;
				let tabIndex = this.tabs.findIndex(t => tabUrlKey(t) === route);
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
			removeTabs(ta) {
				if (!ta || !ta.length) return;
				ta.forEach(tab => {
					let ix = this.tabs.indexOf(tab);
					if (ix == -1) return;
					let rt = this.tabs.splice(ix, 1);
					if (rt.length) {
						this.closedTabs.unshift(rt[0]);
						if (this.closedTabs.length > 10)
							this.closedTabs.pop();
					}
				});
				if (!this.tabs.length)
					this.selectHome(true);
				else {
					let currentTab = this.tabs.find(t => t.key === this.contextTabKey);
					if (this.activeTab !== currentTab)
						this.selectTab(currentTab, true);
				}
				this.storeTabs();
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
			clearLocalStorage() {
				let keys = [];
				for (let f in window.localStorage) {
					if (window.localStorage.hasOwnProperty(f) && f.endsWith('_tabs')) {
						if (f != this.storageKey)
							keys.push(f);
					}
				}
				if (keys.length > 10)
					keys.forEach(f => window.localStorage.removeItem(f));
			},
			storeTabs() {
				var mapTab = (t) => { return { title: t.title, url: t.url, query: t.query || '', parentUrl: t.parentUrl }; };
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
				if (!tabs) {
					this.selectHome(true);
					return;
				}
				try {
					let elems = JSON.parse(tabs);
					let ix = elems.index;
					for (let i = 0; i < elems.tabs.length; i++) {
						let t = elems.tabs[i];
						let loaded = ix === i;
						if (loaded)
							this.navigatingUrl = t.url;
						this.tabs.push({ title: t.title, url: t.url, query: t.query, loaded, key: tabKey++, root: null, parentUrl: t.parentUrl, reload: 0, debug: false });
					}
					for (let i = 0; i < elems.closedTabs.length; i++) {
						let t = elems.closedTabs[i];
						this.closedTabs.push({ title: t.title, url: t.url, query: t.query, loaded: true, key: tabKey++ });
					}
					if (ix >= 0 && ix < this.tabs.length)
						this.activeTab = this.tabs[ix];
					else
						this.selectHome(true);
				} catch (err) {
				}
			},
			toggleTabPopup() {
				eventBus.$emit('closeAllPopups');
				this.tabPopupOpen = !this.tabPopupOpen;
			},
			// context menu
			tabsContextMenu(ev) {
				this.contextTabKey = 0;
				let li = ev.target.closest('li');
				if (li)
					this.contextTabKey = +(li.getAttribute('tab-key') || 0);
				let menu = document.querySelector('#ctx-tabs-popup');
				let br = menu.parentNode.getBoundingClientRect();
				let style = menu.style;
				style.top = (ev.clientY - br.top) + 'px';
				style.left = (ev.clientX - br.left) + 'px';
				menu.classList.add('show');
				//console.dir(this.contextTabKey);
			},
			popupClose() {
				let t = this.tabs.find(t => t.key === this.contextTabKey);
				if (t) this.closeTab(t);
			},
			reopenClosedTab() {
				if (this.closedTabs.length <= 0) return;
				let t = this.closedTabs[0];
				this.reopenTab(t);
			},
			popupCloseOther() {
				if (!this.contextTabKey) return;
				let tabs = this.tabs.filter(t => t.key !== this.contextTabKey);
				this.removeTabs(tabs);
			},
			popupCloseRight() {
				if (!this.contextTabKey) return;
				let tabs = this.tabs.filter(t => t.key > this.contextTabKey);
				this.removeTabs(tabs);
			},
			popupCloseAll() {
				this.selectHome(true);
				let tabs = this.tabs.filter(t => t.key > 10);
				this.removeTabs(tabs);
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
				url = store.replaceUrlQuery(url, prms.query);
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
			_requery(vm, data) {
				if (!vm) return;
				if (vm.__requery) {
					vm.__requery();
				}
				if (!vm.__tabUrl__) return;
				let t = this.tabs.find(t => tabUrlKey(t) === vm.__tabUrl__);
				if (!t) return;
				let run = data ? data.Run : false;
				if (data)
					data.Run = undefined;
				if (!t.loaded)
					return;
				t.url = urlTools.replaceUrlQuery(t.url, data)
				if (t.root)
					t.root.__tabUrl__ = tabUrlKey(t);
				t.reload += run ? 7 : 1;
				this.storeTabs();
			},
			_isModalRequery(arg) {
				if (arg.url && this.modalRequeryUrl && this.modalRequeryUrl === arg.url)
					arg.result = true;
			},
			_modalRequery(baseUrl) {
				let dlg = this._findRealDialog();
				if (!dlg) return;
				let inst = dlg.instance; // include instance
				if (inst && inst.modalRequery) {
					if (baseUrl)
						dlg.url = baseUrl;
					this.modalRequeryUrl = dlg.url;
					inst.modalRequery();
				}
			},
			_modalCreated(instance) {
				// include instance!
				let dlg = this._findRealDialog();
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
			_pageReloaded(path) {
				if (!path) return;
				let seg = path.split('?');
				if (seg.length < 2) return;
				let tab = this.tabs.find(t => '/_page' + t.url === seg[0]);
				if (!tab) return;
				let q = '';
				if (seg[1])
					q = '?' + seg[1];
				if (tab.query === q) return;
				this.lockRoute = true;
				tab.query = q;
				Vue.nextTick(() => {
					this.lockRoute = false;
				});
				this.storeTabs();
			},
			_eventToParentTab(ev) {
				let tab = this.tabs.find(t => t.root === ev.source);
				if (!tab || !tab.root || !tab.parentUrl) return;
				let ptab = this.tabs.find(t => t.url === tab.parentUrl);
				if (ptab && ptab.root)
					ptab.root.$data.$emit(ev.event, ev.data);
			},
			_findRealDialog() {
				// skip alerts, confirm, etc
				for (let i = this.modals.length - 1; i >= 0; --i) {
					let md = this.modals[i];
					if (md.rd) {
						return md;
					}
				}
				return null;
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
			this.clearLocalStorage();
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
			eventBus.$on('modalCreated', this._modalCreated);
			eventBus.$on('requery', this._requery);
			eventBus.$on('isModalRequery', this._isModalRequery);
			eventBus.$on('modalRequery', this._modalRequery);
			eventBus.$on('modalClose', this._eventModalClose);
			eventBus.$on('modalCloseAll', this._eventModalCloseAll);
			eventBus.$on('registerData', this.registerData);
			eventBus.$on('showSidePane', this.showSidePane);
			eventBus.$on('confirm', this._eventConfirm);
			eventBus.$on('closePlain', this.closeTabFromStore);
			eventBus.$on('pageReloaded', this._pageReloaded);
			eventBus.$on('toParentTab', this._eventToParentTab);
			eventBus.$on('closeAllTabs', this.popupCloseAll);
			eventBus.$on('beginRequest', () => {
				me.requestsCount += 1;
				window.__requestsCount__ = me.requestsCount;
			});
			eventBus.$on('endRequest', () => {
				me.requestsCount -= 1;
				window.__requestsCount__ = me.requestsCount;
			});
			eventBus.$on('checkVersion', (ver) => {
				if (ver && this.appData && ver !== this.appData.version)
					this.newVersionAvailable = true;
			});

			signalR.startService();

			window.addEventListener("beforeunload", (ev) => {
				if (!this.hasModified)
					return;
				ev.preventDefault();
				if (ev) ev.returnValue = true;
				return true;
			});
		}
	});
})();

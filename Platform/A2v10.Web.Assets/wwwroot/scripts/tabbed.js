// Copyright © 2023-2026 Oleksandr Kukhtin. All rights reserved.

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
			setTimeout(startService, 5000);
		}
	}
};

// Copyright © 2023-2025 Oleksandr Kukhtin. All rights reserved.

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
				<button v-if=hasLicense @click=license tabindex="-1" class="dropdown-item">
					<i class="ico ico-policy"></i>
					<span v-text="licenseText"></span>
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
			hasLicense: Boolean,
			profileText: String,
			licenseText: String,
			enableDark: Boolean,
			isDark: Boolean,
			logo: String
		},
		computed: {
			locale() { return locale; },
			hasLogo() { return !!this.logo; }
		},
		methods: {
			async logout() {
				let res = await http.post('/account/logout2');
				window.location.assign(`/account/${res.showLogOut ? 'loggedout' : 'login'}`);
			},
			profile() {
				eventBus.$emit('navigateto', { url: '/_profile/index/0'});
			},
			license() {
				const dlgData = { promise: null, rd: true, raw: true };
				eventBus.$emit('modal', '/viewlicense', dlgData);
			},
			async setTheme(theme) {
				let res = await fetch(`/account/darkmode?theme=${theme}`, { method: 'POST' });
				if (res.ok)
					window.location.reload();
			}
		},
		mounted() {
		}
	});
})();
// Copyright © 2023-2026 Oleksandr Kukhtin. All rights reserved.

/*20260814-8650*/
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
		<ul :style="mdiMenuStyle" :class="mdiMenuClass">
			<li v-for="m in activeMenu.Menu" class="level-0" :style="lev0Style(m)">
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
			columns: Number
		},
		data() {
			return {
				activeMenu: null
			};
		},
		computed: {
			isMenuVisible() {
				return !!this.activeMenu;
			},
			mdiMenuClass() {
				return this.columns > 1 ? 'mdi-menu-grid' : '';
			},
			mdiMenuStyle() {
				if (this.columns > 1)
					return { 'grid-template-columns': "auto ".repeat(this.columns) };	
				return {};
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
			lev0Style(m) {
				let s = {};
				if (m.Row > 0)
					s['grid-row'] = '' + m.Row;
				if (m.Col > 0)
					s['grid-column'] = '' + m.Col;
				return s;
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

// Copyright © 2023-2026 Oleksandr Kukhtin. All rights reserved.

/*20260806-8650*/

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
	const MAX_OPENED_TABS = 10;

	let tabKey = 77;
	
	const tabUrlKey = tab => `${tab.url}:${tab.key}`;

	function getClosestLi(ev) {
		if (!ev.target) return null;
		return ev.target.closest('li');
	}

	function getTabUrl(tab) {
		if (!tab) return '';
		let host = `${window.location.protocol}//${window.location.host}`;
		let concatChar = tab.url.includes('?') ? '&' : '?';
		let encQuery = '';
		if (tab.query)
			encQuery = `${concatChar}_xq=${encodeURIComponent(tab.query)}`;
		return `${host}${tab.url}${encQuery}`;
	}

	function intersectLines(t, c) {
		let dx = 0;
		if (t.r > c.r)
			dx = c.r - t.l;
		else if (t.l < c.l)
			dx = t.r - c.l;
		return dx > 0;
	}

	app.components["std:shellPlain"] = Vue.extend({
		store,
		data() {
			return {
				tabs: [],
				closedTabs: [],
				activeTab: null,
				maxUsed: 0,
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
				homeRoot: null,
				homeLoaded: false,
				lockRoute: false,
				requestsCount: 0,
				contextTabKey: 0,
				newVersionAvailable: false,
				movedTab: null
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
			replaceState(tab) {
				//window.history.replaceState(null, null, tab ? tab.url : '/');
			},
			navigate(u1) {
				if (u1.url.indexOf('{genrandom}') >= 0) {
					let randomString = Math.random().toString(36).substring(2);
					u1.url = u1.url.replace('{genrandom}', randomString);
				}
				let tab = this.tabs.find(tab => tab.url == u1.url);
				if (!tab) {
					let parentUrl = '';
					if (this.activeTab)
						parentUrl = this.activeTab.url || '';
					tab = { title: u1.title, url: u1.url, query: u1.query || '', cnt: 1, o: this.tabs.length + 1, loaded: true, key: tabKey++, root: null, parentUrl: parentUrl, reload: 0, debug: false };
					this.tabs.push(tab);
					var cti = this.closedTabs.findIndex(t => t.url === u1.url);
					if (cti >= 0)
						this.closedTabs.splice(cti, 1);
				}
				if (tab.query !== u1.query) {
					tab.query = u1.query;
					tab.reload += 1;
				}
				tab.loaded = true;
				this.activeTab = tab;
				this.useTab(tab);
				if (this.tabs.length > MAX_OPENED_TABS) {
					let mt = this.tabs.toSorted((a, b) => b.cnt - a.cnt);
					if (mt.length) {
						let ix = this.tabs.indexOf(mt[0]);
						this.tabs.splice(ix, 1);
					}
				}
				this.storeTabs();
			},
			navigateUrl(url, query) {
				this.navigatingUrl = url;
				this.navigate({ url: url, title: '', query: query || "" });
			},
			navigateTo(to) {
				this.navigatingUrl = to.url;
				this.navigate({ url: to.url, title: '' });
			},
			dummy() { },
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
			async tabLoadError(url, err) {
				if (err) err = err.replace('\\n', '\n');
				let tab = this.activeTab || this.tabs.find(t => t.url == url);
				if (tab) {
					if (err.indexOf('UI:') === 0)
						await this._alert(err.substring(3));
					else
						alert(err);
					if (tab.root && tab.root.$onError) {
						tab.root.$onError();
						return;
					}
					this.closeTab(tab);
				} else
					alert(err);
			},
			tabLoadComplete(page) {
				if (page) {
					let tab = this.activeTab || this.tabs.find(t => t.url == page.src);
					if (tab) {
						this.replaceState(tab);
						tab.root = page.root;
						document.title = tab.title;
						if (page.root) {
							page.root.__tabUrl__ = tabUrlKey(tab);
							page.root.$store.commit('setroute', tab.url);
						}
					} else if (page.src === '/_home/index/0')
						this.homeRoot = page.root;
				}
				this.navigatingUrl = '';
			},
			isTabActive(tab) {
				return tab === this.activeTab;
			},
			isHomeActive() {
				return !this.activeTab;
			},
			clickTooltip(tab) {
				let url = getTabUrl(tab);
				navigator.clipboard.writeText(url);
			},
			tabTooltip(tab) {
				return `${tab.url}`;
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
				return tab.loaded ? (tab.url + (tab.query || '')) : null;
			},
			homeSource() {
				return this.homeLoaded ? '/_home/index/0' : null;
			},
			useTab(tab) {
				this.tabs.filter(t => t != tab).forEach(t => t.cnt += 1);
				tab.cnt = 1;
				this.maxUsed = Math.max(...this.tabs.map(t => t.cnt));
			},
			selectHome(noStore) {
				this.homeLoaded = true;
				this.activeTab = null;
				document.title = this.homePageTitle;
				this.replaceState(null);
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
				if (this.activeTab != tab)
					this.useTab(tab);
				this.activeTab = tab;
				document.title = tab.title;
				this.replaceState(tab);
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
						if (this.closedTabs.length > MAX_OPENED_TABS)
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
				this._resortTabs();
				this.storeTabs();
			},
			removeTab(tabIndex) {
				let currentTab = this.tabs[tabIndex];
				let parent = this.tabs.find(t => t.url === currentTab.parentUrl);
				if (parent)
					this.selectTab(parent, true);
				if (this.isTabActive(currentTab)) {
					if (tabIndex > 0)
						this.selectTab(this.tabs[tabIndex - 1], true);
					else if (this.tabs.length > 1)
						this.selectTab(this.tabs[tabIndex + 1], true);
					else
						this.selectHome(true);
				}
				let rt = this.tabs.splice(tabIndex, 1);
				if (rt.length) {
					this.closedTabs.unshift(rt[0]);
					if (this.closedTabs.length > MAX_OPENED_TABS)
						this.closedTabs.pop();
				}
				this._resortTabs();
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
				this.maxUsed = Math.max(...this.tabs.map(t => t.cnt));
				var mapTab = (t) => { return { title: t.title, url: t.url, cnt: t.cnt, query: t.query || '', parentUrl: t.parentUrl }; };
				let ix = this.tabs.indexOf(this.activeTab);
				let tabs = JSON.stringify({
					index: ix,
					tabs: this.tabs.map(mapTab),
					closedTabs: this.closedTabs.map(mapTab),
				});
				window.localStorage.setItem(this.storageKey, tabs);
			},
			restoreTabs(path) {
				let tabs = window.localStorage.getItem(this.storageKey);
				if (!tabs) {
					this.selectHome(true);
					return;
				}
				try {
					let elems = JSON.parse(tabs);
					let ix = elems.index;
					if (path !== '/') {
						let f = elems.tabs.findIndex(t => t.url === path);
						if (f >= 0)
							ix = f;
					}
					let len = elems.tabs.length;
					for (let i = 0; i < len; i++) {
						let t = elems.tabs[i];
						let loaded = ix === i;
						if (loaded)
							this.navigatingUrl = t.url;
						this.tabs.push({
							title: t.title, url: t.url, query: t.query,
							cnt: t.cnt || len - i - 1, o: i + 1, loaded, key: tabKey++, root: null, parentUrl: t.parentUrl, reload: 0, debug: false
						});
					}
					for (let i = 0; i < elems.closedTabs.length; i++) {
						let t = elems.closedTabs[i];
						this.closedTabs.push({ title: t.title, url: t.url, query: t.query, cnt: 1, loaded: true, key: tabKey++ });
					}
					if (ix >= 0 && ix < this.tabs.length)
						this.activeTab = this.tabs[ix];
					else
						this.selectHome(true);
				} catch (err) {
				}
				this.maxUsed = Math.max(...this.tabs.map(t => t.cnt));
			},
			toggleTabPopup() {
				eventBus.$emit('closeAllPopups');
				this.tabPopupOpen = !this.tabPopupOpen;
			},
			// context menu
			tabsContextMenu(ev) {
				eventBus.$emit('closeAllPopups');
				this.contextTabKey = 0;
				let li = ev.target.closest('li');
				if (li)
					this.contextTabKey = +(li.getAttribute('tab-key') || 0);
				let menu = document.querySelector('#ctx-tabs-popup');
				let br = menu.parentNode.getBoundingClientRect();
				let mw = 242; // menu width
				let lp = ev.clientX - br.left;
				if (lp + mw > br.right)
					lp -= mw;
				let style = menu.style;
				style.top = (ev.clientY - br.top) + 'px';
				style.left = lp + 'px';
				menu.classList.add('show');
			},
			popupClose() {
				let t = this.tabs.find(t => t.key === this.contextTabKey);
				if (t) this.closeTab(t);
			},
			copyTabUrl() {
				let t = this.tabs.find(t => t.key === this.contextTabKey);
				if (!t) return;
				window.navigator.clipboard.writeText(getTabUrl(t));
				if (t.root && t.root.$toast)
					t.root.$toast("URL copied to clipboard");
			},
			reopenClosedTab() {
				if (this.closedTabs.length <= 0) return;
				let t = this.closedTabs[0];
				this.reopenTab(t);
			},
			_resortTabs() {
				let arr = this.tabs;
				arr.sort((a, b) => a.o - b.o);
				for (let i = 0; i < arr.length; i++)
					arr[i].o = i + 1;
				this.storeTabs();
			},
			offsetLeft(t) {
				let mt = this.movedTab;
				if (!mt || mt.tab !== t)
					return "0";
				return mt.pos + 'px';
			},
			isDragged(t) {
				let mt = this.movedTab;
				return mt && mt.tab === t;
			},
			willClose(t) {
				return this.tabs.length >= MAX_OPENED_TABS && t.cnt === this.maxUsed;
			},
			pointerDown(ev, t) {
				if (ev.target.closest('.close')) return; // the close button is not a drag handle
				let li = getClosestLi(ev)
				if (!li) return;
				ev.target.setPointerCapture(ev.pointerId);
				let cr = li.getBoundingClientRect();
				this.movedTab = { tab: t, x: ev.x - cr.left, l: cr.left, pos: 0 };
				this._dragContext = { tabs: [], currentTab: -1, pointTab: -1, left: 0 };
			},
			pointerUp(ev, t) {
				if (!this.movedTab) return;
				ev.target.releasePointerCapture(ev.pointerId);
				this.movedTab = null;
				this._dragContext = null;
				this._resortTabs();
			},
			pointerMove(ev, t) {
				let mt = this.movedTab
				if (!mt) return;

				let offset = ev.x - mt.l - mt.x;
				mt.pos = offset;

				let li = getClosestLi(ev)
				if (!li)
					return;
				let cr = li.getBoundingClientRect();
				let hw = cr.width / 4;

				if (Math.abs(offset) < hw)
					return;

				this._createOrderedTabs();

				let testTab = { l: ev.x - mt.x, r: ev.x - mt.x + cr.width };

				if (!this._testOrderedTabs(testTab))
					return;

				let nl = this._swapOrderedTabs(testTab);
				if (!nl) return;
				//console.log('set nl:', nl.l, 'i:', nl.i);
				mt.l = nl.l;
				offset = ev.x - mt.l - mt.x;
				mt.pos = offset;
			},
			_createOrderedTabs() {
				if (!this._dragContext) return;
				if (this._dragContext.tabs.length) return;
				if (!this.tabs.length) return;

				let findAttr = (t) => {
					return this.$refs.tab.find(el => el.getAttribute('tab-key') == t.key);
				};

				let otabs = this.tabs.map((t, ix) => ({
					tab: t,
					ref: findAttr(t), w: 0,
				}));
				let hcr = this.$refs.home.getBoundingClientRect();
				this._dragContext.left = hcr.right;
				let l = hcr.right;
				for (let i = 0; i < otabs.length; i++) {
					let t = otabs[i];
					if (t.tab === this.movedTab.tab)
						this._dragContext.currentTab = i;
					let cr = t.ref.getBoundingClientRect();
					t.w = cr.width;
					l += t.w;
				}
				this._dragContext.tabs = otabs;
				return otabs;
			},
			_testOrderedTabs(testTab) {
				if (!this.movedTab) return false;
				let otabs = this._dragContext.tabs;
				if (!otabs || !otabs.length) return false;
				let l = this._dragContext.left;
				for (let i = 0; i < otabs.length; i++) {
					let t = otabs[i];
					if (t.tab === this.movedTab.tab)
						this._dragContext.currentTab = i;
					if (intersectLines(testTab, { l, r: l + t.w })) {
						this._dragContext.pointTab = i;
					}
					l += t.w;
				}
				return this._dragContext.currentTab != -1 &&
					this._dragContext.pointTab != -1 &&
					this._dragContext.currentTab != this._dragContext.pointTab;
			},
			_swapOrderedTabs(testTab) {
				if (!this._dragContext) return;
				let tabs = this._dragContext.tabs;
				if (!tabs.length) return;
				let tab1 = tabs[this._dragContext.currentTab];
				let tab2 = tabs[this._dragContext.pointTab];
				if (tab1 === tab2) return;
				if (tab1.tab.o === tab2.tab.o)
					return;
				let tmp = tab1.tab.o;
				tab1.tab.o = tab2.tab.o;
				tab2.tab.o = tmp;
				let l = this._dragContext.left;
				let neo = tabs.toSorted((a, b) => a.tab.o - b.tab.o);
				this._dragContext.tabs = neo;
				for (let i = 0; i < neo.length; i++) {
					let t = neo[i];
					if (l >= testTab.l) { 
						this._dragContext.pointTab = i;
						return { l, i };
					}
					l += t.w;
				}
				return { l, i: neo.length }; 
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
			_modalSetAttribites(attr, instance) {
				if (!attr || !instance) return;
				let dlg = this._findRealDialog();
				if (!dlg) return;
				dlg.attrs = instance.__parseControllerAttributes(attr);
			},
			_modalCreated(instance) {
				// include instance!
				let dlg = this._findRealDialog();
				if (!dlg) return;
				dlg.instance = instance;
			},
			_activeTabUrl(s) {
				if (!s) return false;
				s.url = getTabUrl(this.activeTab);
				return true;
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
					else if (canResult && canResult.then) {
						canResult.then(function (innerResult) {
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
			_alert(msg) {
				let dlgData = {
					promise: null, data: {
						message: msg, style: 'alert'
					}
				};
				this._eventConfirm(dlgData);
				return dlgData.promise;
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
				if (!root || !root.$data)
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
				else
					this.updateModelStack(this.homeRoot);
			}
		},
		mounted() {
			popup.registerPopup(this.$el);
			// home page here this.tabs.push({})
			this.$el._close = this.__clickOutside;
			this.clearLocalStorage();
			if (!this.menu || !this.menu.length)
				this.selectHome(false); // store empty tabs
			else {
				this.restoreTabs(window.location.pathname);
				if (window.location.pathname !== '/') {
					let p = window.location.pathname;
					let q = '';
					let usp = new URLSearchParams(window.location.search);
					if (usp.has('_xq')) {
						q = usp.get('_xq');
						usp.delete('_xq');
					}
					window.history.replaceState(undefined, undefined, '/');
					let sp = usp.toString();
					if (sp)
						p += '?' + sp;
					// path = url + normal query, query from _xq!
					this.navigateUrl(p, q);
				}
			}
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
			eventBus.$on('modalSetAttribites', this._modalSetAttribites);
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
			eventBus.$on('activeTabUrl', this._activeTabUrl);
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
				if (ver && this.appData && (ver.app !== this.appData.version || ver.module !== this.appData.moduleVersion))
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

// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

(function () {

	const TODAY_COLOR = "#fffff080"; // sync with LESS!
	const locale = window.$$locale;
	const dateLocale = locale.$DateLocale || locale.$Locale;
	const monthLocale = locale.$Locale; // for text

	let utils = require('std:utils');
	let tu = utils.text;
	let du = utils.date;

	Vue.component('a2-big-calendar', {
		template: `
<div class="a2-big-calendar">
<div class="top-bar">
	<div class="toolbar">
		<button class="btn btn-tb btn-icon" @click="nextPart(-1)"><i class="ico ico-arrow-left"></i></button>
		<button class="btn btn-tb btn-icon" @click="nextPart(1)"><i class="ico ico-arrow-right"></i></button>
		<button class="btn btn-tb btn-icon" @click="todayPart"><i class="ico ico-calendar-today"></i> <span v-text="locale.$Today"></span></button>
		<slot name="leftbar"></slot>
	</div>
	<div class="title">
		<span v-text=topTitle></span>
	</div>
	<div class="toolbar">
		<button class="btn btn-tb btn-icon" @click="setView('week')"><i class="ico ico-calendar-week"></i> <span v-text="locale.$Week"></span></button>
		<button class="btn btn-tb btn-icon" @click="setView('month')"><i class="ico ico-calendar"></i> <span v-text="locale.$Month"></span></button>
		<slot name="topbar"></slot>
	</div>
</div>
<div v-if="isView('month')" class="bc-month-conainer bc-container" ref="mc">
<table class="bc-month bc-table">
	<thead>
		<tr class="weekdays"><th v-for="d in 7" v-text="wdTitle(d)"></th></tr>
	</thead>
	<tbody>
		<tr v-for="row in days">
			<td v-for="day in row" class=bc-day :class="dayClass(day)" @click.stop.prevent="clickDay(day)">
				<div v-text="day.getDate()" class="day-date"></div>
				<div class="bc-day-body">
					<div v-for="(ev, ix) in dayEvents(day)" :key=ix class="day-event"
							:style="{backgroundColor: ev.Color || ''}" @click.stop.prevent="clickEvent(ev)">
						<slot name="monthev" v-bind:item="ev" class="me-body">
							<span class="me-body" v-text="eventTitle(ev)" :title="eventTitle(ev)"/>
						</slot>
					</div>
				</div>
			</td>
		</tr>
	</tbody>
</table>
</div>
<div v-if="isView('week')" class="bc-week-container bc-container" ref=wc>
	<table class="bc-week bc-table">
		<colgroup>
			<col style="width:2%"/>
			<col v-for="d in 7" style="width:14%" :style="weekColumnStyle(d)">
		</colgroup>
		<thead>
			<tr class="weekdays">
				<th></th>
				<th v-for="d in 7" v-text="wdWeekTitle(d)" :class="{today: isWeekDayToday(d)}"></th>
			</tr>
		</thead>
		<tbody>
			<template v-for="h in 24">
				<tr>
					<th rowspan=2>
						<span v-text="hoursText(h - 1)" class="h-title"></span>
					</th>
					<td v-for="d in 7" class="bc-h-top" @click.stop.prevent="clickHours(d, h, false)">
						<div class="h-event" v-for="(ev, ix) in hourEvents(d, h-1, false)" :key=ix
								:style="hourStyle(ev, false)" @click.stop.prevent="clickEvent(ev)">
							<slot name="weekev" v-bind:item="ev">
								<span class="h-ev-body" v-text="eventTitle(ev)" :title="eventTitle(ev)"></span>
							</slot>
						</div>
					</td>
				</tr>
				<tr>
					<td v-for="d in 7" class="bc-h-bottom" @click.stop.prevent="clickHours(d, h, true)">
						<div class="h-event" v-for="(ev, ix) in hourEvents(d, h-1, true)" :key=ix
							:style="hourStyle(ev, true)" @click.stop.prevent="clickEvent(ev)">
							<slot name="weekev" v-bind:item="ev">
								<span class="h-ev-body" v-text="eventTitle(ev)" :title="eventTitle(ev)"></span>
							</slot>
						</div>
					</td>
				</tr>
			</template>
		</tbody>
	</table>
	<div class="current-time" :style="currentTimeStyle()" :key=updateKey></div>
</div>
</div>
`,
		props: {
			item: Object,
			prop: String,
			viewItem: Object,
			viewProp: String,
			events: Array,
			delegates: Object
		},
		data() {
			return {
				updateKey: 1,
				timerId: 0
			}
		},
		computed: {
			locale() {
				return locale;
			},
			modelDate() {
				return this.item[this.prop];
			},
			modelView() {
				return this.viewItem[this.viewProp];
			},
			days() {
				let dt = new Date(this.modelDate);
				let d = dt.getDate();
				dt.setDate(1); // 1-st day of month
				let w = dt.getDay() - 1; // weekday
				if (w === -1) w = 6;
				//else if (w === 0) w = 7;
				dt.setDate(-w + 1);
				let arr = [];
				for (let r = 0; r < 6; r++) {
					let row = [];
					for (let c = 0; c < 7; c++) {
						row.push(new Date(dt));
						dt.setDate(dt.getDate() + 1);
					}
					arr.push(row);
				}
				return arr;
			},
			topTitle() {
				let m = this.modelDate.toLocaleDateString(monthLocale, { month: "long" });
				return `${tu.capitalize(m)} ${this.modelDate.getFullYear()}`;
			},
			firstMonday() {
				let wd = this.modelDate.getDay();
				if (wd == 1) return this.modelDate;
				if (!wd) wd = 7;
				return du.add(this.modelDate, -(wd - 1), 'day');
			}
		},
		methods: {
			isView(view) {
				return this.viewItem[this.viewProp] === view;
			},
			wdTitle(d) {
				let dt = this.days[0][d - 1];
				return dt.toLocaleString(monthLocale, { weekday: "long" });
			},
			wdWeekTitle(d) {
				let fd = du.add(this.firstMonday, d - 1, 'day');
				let wd = fd.toLocaleString(monthLocale, { weekday: 'long' });
				let day = fd.toLocaleString(monthLocale, { month: "long", day: 'numeric' });
				return `${wd}, ${day}`;
			},
			dayClass(day) {
				let cls = '';
				if (du.equal(day, du.today()))
					cls += ' today';
				if (day.getMonth() !== this.modelDate.getMonth())
					cls += " other";
				return cls;
			},
			dayEvents(day) {
				return this.events.filter(e => du.equal(e.Date, day));
			},
			hoursText(h) {
				return `${h}:00`;
			},
			hourStyle(ev, h2) {
				let min = ev.Date.getMinutes();
				let s = {
					backgroundColor: ev.Color,
					height: `${ev.Duration}px`,
					top: `${h2 ? min - 30 : min}px`
				};
				return s;
			},
			hourEvents(dno, hour, h2) {
				let day = du.add(this.firstMonday, dno - 1, 'day');
				let inside = (ev) => {
					if (!du.equal(ev.Date, day)) return false;
					let h = ev.Date.getHours();
					let m = ev.Date.getMinutes();
					if (h2)
						return h >= hour && h < hour + 1 && m >= 30;
					else
						return h >= hour && h < hour + 1 && m < 30;
				}
				return this.events.filter(inside);
			},
			nextPart(d) {
				let dt = new Date(this.modelDate);
				if (this.isView('week'))
					dt = du.add(dt, 7 * d, 'day');
				else
					dt.setMonth(dt.getMonth() + d);
				this.item[this.prop] = dt;
			},
			todayPart() {
				let dt = new Date();
				this.item[this.prop] = dt;
			},
			setView(view) {
				this.viewItem[this.viewProp] = view;
			},
			clickEvent(ev) {
				if (this.delegates && this.delegates.ClickEvent)
					this.delegates.ClickEvent(ev);
			},
			clickDay(day) {
				if (!this.delegates || !this.delegates.ClickDay) return;
				let dt = new Date(day);
				dt.setHours(0);
				dt.setMinutes(0);
				this.delegates.ClickDay(day);
			},
			clickHours(d, h, h2) {
				if (!this.delegates || !this.delegates.ClickDay) return;
				let fmd = this.firstMonday;
				let dt = du.add(fmd, d - 1, 'day');
				dt.setMinutes(h2 ? 30 : 0);
				dt.setHours(h - 1);
				this.delegates.ClickDay(dt);
			},
			currentTimeStyle() {
				let dt = new Date();
				let x = dt.getHours() * 60 + dt.getMinutes() + 35 - 1 /*thead*/;
				return {
					top: `${x}px`,
					'--time': `"${dt.toLocaleTimeString(dateLocale, { hour: '2-digit', minute: '2-digit' })}"`
				};
			},
			isWeekDayToday(d) {
				let day = du.add(this.firstMonday, d - 1, 'day');
				return du.equal(day, du.today());
			},
			weekColumnStyle(d) {
				if (this.isWeekDayToday(d))
					return { backgroundColor: TODAY_COLOR };
				return undefined;
			},
			eventTitle(ev) {
				let time = ev.Date.toLocaleTimeString(dateLocale, { hour: '2-digit', minute: '2-digit' });
				return `${time} ${ev.Name}`;
			},
			__fitScroll() {
				if (this.isView('month')) {
					setTimeout(() => {
						this.$refs.mc.scrollTop = 0;
					}, 0);
				} else
					setTimeout(() => {
						let rows = this.$refs.wc.getElementsByTagName('tr');
						rows[8 * 2 + 1].scrollIntoView(true);
					}, 0);
			}
		},
		watch: {
			modelView() {
				this.__fitScroll();
			}
		},
		mounted() {
			this.__fitScroll();
			this.timerId = setInterval(() => {
				if (this.isView('week')) {
					this.updateKey++;
				}
			}, 10 * 1000);
		},
		destroyed() {
			if (this.timerId)
				clearInterval(this.timerId);
		}
	});
})();

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
			placeholder: String,
			fetchData: Object
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
					let invokeArg = Object.assign({}, { Text: this.query }, this.fetchData);
					let el = await this.$root.$invoke('fetch', invokeArg, this.url);
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
				let be = await vm.$showDialog(this.url + '/browse', null, this.fetchData);
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

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

// A comment: own contenteditable, the text is markdown of three elements — \n, **bold**, ![name](src).
// Decisions — design/texteditor.md.

(function () {

	const IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'];

	function escapeHtml(s) {
		return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
	}

	function escapeMd(s) {
		return s.replace(/[\\*]/g, '\\$&');
	}

	// One rule for a link: the view makes it clickable, the editor highlights it. Trailing punctuation belongs to the sentence
	function* findLinks(s) {
		for (let m of s.matchAll(/https?:\/\/[^\s<>"]+/g))
			yield { index: m.index, url: m[0].replace(/[.,;:!?)]+$/, '') };
	}

	function textHtml(s, view) {
		if (!view)
			return escapeHtml(s);
		let out = '';
		let last = 0;
		for (let { index, url } of findLinks(s)) {
			out += escapeHtml(s.substring(last, index));
			out += `<a href="${escapeHtml(url)}" target="_blank" rel="noopener">${escapeHtml(url)}</a>`;
			last = index + url.length;
		}
		return out + escapeHtml(s.substring(last));
	}

	// The links of the editor's text, as ranges over its text nodes: the highlight paints them, a click opens them — one rule.
	// A link cut by bold into two text nodes is not a link here — accepted
	function linkRanges(el) {
		let ranges = [];
		let walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
		for (let n = walker.nextNode(); n; n = walker.nextNode()) {
			for (let { index, url } of findLinks(n.nodeValue)) {
				let r = new Range();
				r.setStart(n, index);
				r.setEnd(n, index + url.length);
				ranges.push(r);
			}
		}
		return ranges;
	}

	// by the rects, not by the caret: a caret lands on the nearest boundary, a click past the end of a line would open the link
	function hit(range, x, y) {
		return Array.from(range.getClientRects()).some(c => x >= c.left && x <= c.right && y >= c.top && y <= c.bottom);
	}

	// The CSS Custom Highlight API paints over the text: the DOM, the caret and undo stay as they are.
	// The registry is global, so editors share one Highlight, each holding its own ranges. No API — no highlight
	const HIGHLIGHT = 'a2-link';

	function linkHighlight() {
		if (!window.CSS || !CSS.highlights) return null;
		let hl = CSS.highlights.get(HIGHLIGHT);
		if (!hl) {
			hl = new Highlight();
			CSS.highlights.set(HIGHLIGHT, hl);
		}
		return hl;
	}

	const IMAGE_RX = /^!\[((?:\\.|[^\]\\])*)\]\(([^)\s]+)\)/;

	// The one render: the view and the text loaded into the editor. Everything is escaped; the output is own markup only.
	// An image is an <img> only if imageUrl gives it a URL — otherwise text (a spy pixel); data-src keeps the stored form.
	// view: links clickable, an image is a link to its original (max-height cuts it) and lazy.
	// .loading — a fixed block with a spinner in the background until load/error (from Azure it takes seconds); on the img itself,
	// no wrapper: a wrapper in contenteditable breaks editing, and the editor loads images too
	function toHtml(md, imageUrl, view) {
		let out = '';
		let text = '';
		let bold = false;
		const flush = () => { out += textHtml(text, view); text = ''; };
		for (let i = 0; i < md.length;) {
			let ch = md[i];
			if (ch === '\\' && i + 1 < md.length) {
				text += md[i + 1];
				i += 2;
			} else if (ch === '*' && md[i + 1] === '*' && (bold || hasClose(md, i + 2))) {
				flush();
				out += bold ? '</b>' : '<b>';
				bold = !bold;
				i += 2;
			} else if (ch === '\n') {
				flush();
				out += '<br>';
				i++;
			} else if (ch === '!' && md[i + 1] === '[') {
				let m = IMAGE_RX.exec(md.substring(i));
				let url = m ? imageUrl(m[2]) : null;
				if (url) {
					flush();
					let name = m[1].replace(/\\(.)/g, '$1');
					let href = escapeHtml(url);
					let img = `<img src="${href}" data-src="${escapeHtml(m[2])}" alt="${escapeHtml(name)}" class="loading"`;
					out += view ? `<a href="${href}" target="_blank" rel="noopener">${img} loading="lazy"></a>` : `${img}>`;
					i += m[0].length;
				} else {
					text += ch;
					i++;
				}
			} else {
				text += ch;
				i++;
			}
		}
		flush();
		return out;
	}

	// A hook is called as a template's function is: this = the model of the page (as emptyPanelCallback). No hook — undefined
	function hook(vm, name, ...args) {
		let d = vm.delegates;
		return d && d[name] ? d[name].apply(vm.$root.$data, args) : undefined;
	}

	// load/error do not bubble — the root sees them on capture. An error too: a broken image, not an endless spinner
	function loaded(ev) {
		if (ev.target.tagName === 'IMG')
			ev.target.classList.remove('loading');
	}

	// an unpaired ** is text
	function hasClose(md, i) {
		for (; i < md.length; i++) {
			if (md[i] === '\\')
				i++;
			else if (md[i] === '*' && md[i + 1] === '*')
				return true;
		}
		return false;
	}

	// Enter: Chrome makes a div, Firefox a br. The last br of a block is Chrome's stub of an empty line.
	// An img without data-src (still uploading — blob:) is not written: the model never holds a dead address.
	function toMarkdown(root) {
		let out = '';
		let breakAfterBlock = false;
		const emit = (s) => {
			if (breakAfterBlock) {
				out += '\n';
				breakAfterBlock = false;
			}
			out += s;
		};
		const walk = (node, inBold) => {
			for (let n = node.firstChild; n; n = n.nextSibling) {
				if (n.nodeType === Node.TEXT_NODE) {
					if (n.nodeValue)
						emit(escapeMd(n.nodeValue.replace(/\u00a0/g, ' ')));
					continue;
				}
				if (n.nodeType !== Node.ELEMENT_NODE)
					continue;
				switch (n.tagName) {
					case 'BR':
						if (!n.nextSibling && (node.tagName === 'DIV' || node.tagName === 'P'))
							break;
						emit('\n');
						break;
					case 'B':
					case 'STRONG': {
						if (inBold) {
							walk(n, true);
							break;
						}
						emit('');
						let start = out.length;
						out += '**';
						walk(n, true);
						// an empty bold is skipped
						if (out.length === start + 2)
							out = out.substring(0, start);
						else
							out += '**';
						break;
					}
					case 'DIV':
					case 'P':
						if (out)
							out += '\n';
						breakAfterBlock = false;
						walk(n, inBold);
						breakAfterBlock = true;
						break;
					case 'IMG': {
						let src = n.getAttribute('data-src');
						if (src)
							emit(`![${(n.alt || '').replace(/[\\\]]/g, '\\$&')}](${src.replace(/[\s)]/g, encodeURIComponent)})`);
						break;
					}
					default:
						walk(n, inBold);
				}
			}
		};
		walk(root, false);
		return out.replace(/\n+$/, '');
	}

	function images(files) {
		return Array.from(files || []).filter(f => IMAGE_TYPES.includes(f.type));
	}

	// a screenshot from the clipboard is always image.png
	function named(file) {
		if (!/^image\.\w+$/i.test(file.name))
			return file;
		let d = new Date();
		let p = (n) => String(n).padStart(2, '0');
		let stamp = `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}-${p(d.getMinutes())}-${p(d.getSeconds())}`;
		return new File([file], `Знімок ${stamp}.${file.name.split('.').pop()}`, { type: file.type });
	}

	function caretAt(x, y) {
		let r = null;
		if (document.caretPositionFromPoint) {
			let p = document.caretPositionFromPoint(x, y);
			if (p) {
				r = document.createRange();
				r.setStart(p.offsetNode, p.offset);
			}
		} else if (document.caretRangeFromPoint)
			r = document.caretRangeFromPoint(x, y);
		if (!r) return;
		r.collapse(true);
		let sel = window.getSelection();
		sel.removeAllRanges();
		sel.addRange(r);
	}

	Vue.component('a2-text-editor', {
		template: `
<div class="a2-text-editor" :contenteditable="disabled ? 'false' : 'true'" :data-placeholder="placeholder"
	@input="sync" @keydown="keydown" @focus="focus" @paste="paste" @click="open" @load.capture="loaded" @error.capture="loaded"
	@dragstart="dragInside(true)" @dragend="dragInside(false)" @drop="drop"></div>
`,
		props: {
			item: Object,
			prop: String,
			placeholder: String,
			disabled: Boolean,
			delegates: Object
		},
		computed: {
			value() {
				return this.item[this.prop] || '';
			}
		},
		watch: {
			// own input is never re-rendered — the caret stays
			value(v) {
				if (v === this.md) return;
				this.render(v);
			}
		},
		methods: {
			loaded,
			imageUrl(src) {
				return hook(this, 'ImageUrl', src) || null;
			},
			render(md) {
				this.md = md;
				this.$el.innerHTML = toHtml(md, src => this.imageUrl(src), false);
				this.highlight();
			},
			highlight() {
				let hl = linkHighlight();
				if (!hl) return;
				for (let r of this.ranges)
					hl.delete(r);
				this.ranges = this.gone ? [] : linkRanges(this.$el);
				for (let r of this.ranges)
					hl.add(r);
			},
			// Ctrl/⌘+click opens an image or a link, as a click in the view does; a plain click edits (places the caret,
			// selects an image). A disabled editor cannot be edited, so any click opens. An image still uploading
			// (blob:, no data-src) has nothing to open yet
			open(ev) {
				if (!(ev.ctrlKey || ev.metaKey) && !this.disabled)
					return;
				let url = null;
				let t = ev.target;
				if (t.tagName === 'IMG') {
					if (t.getAttribute('data-src'))
						url = t.src;
				} else {
					let link = linkRanges(this.$el).find(r => hit(r, ev.clientX, ev.clientY));
					if (link)
						url = link.toString();
				}
				if (!url)
					return;
				ev.preventDefault();
				window.open(url, '_blank', 'noopener');
			},
			sync() {
				if (this.gone) return;
				let el = this.$el;
				// Chrome leaves a <br> in an emptied editor — :empty and the placeholder need it gone
				if (!el.textContent && !el.querySelector('img'))
					el.innerHTML = '';
				this.md = toMarkdown(el);
				this.highlight();
				if (this.value !== this.md)
					this.item[this.prop] = this.md;
			},
			focus() {
				// Ctrl+B → <b>, not <span style>
				document.execCommand('styleWithCSS', false, false);
			},
			keydown(ev) {
				if (!(ev.ctrlKey || ev.metaKey)) return;
				if (ev.key === 'Enter') {
					ev.preventDefault();
					hook(this, 'Submit');
				} else if (ev.code === 'KeyI' || ev.code === 'KeyU')
					ev.preventDefault(); // italic and underline would be lost on save
			},
			canUpload() {
				return !!(this.delegates && this.delegates.UploadImage);
			},
			paste(ev) {
				let dt = ev.clipboardData;
				if (!dt) return;
				ev.preventDefault();
				let files = this.canUpload() ? images(dt.files) : [];
				if (files.length)
					this.insertImages(files);
				else
					this.insertText(dt.getData('text/plain'));
			},
			// dragstart fires only for a drag that began inside the editor
			dragInside(on) {
				this.moving = on;
			},
			drop(ev) {
				if (this.moving) return; // a move inside the editor is the browser's
				ev.preventDefault();
				let dt = ev.dataTransfer;
				this.$el.focus();
				caretAt(ev.clientX, ev.clientY);
				let files = this.canUpload() ? images(dt.files) : [];
				if (files.length)
					this.insertImages(files);
				else
					this.insertText(dt.getData('text/plain'));
			},
			insertText(text) {
				// foreign HTML never enters
				if (text)
					document.execCommand('insertText', false, text);
			},
			insertImages(files) {
				let items = files.map(f => {
					let file = named(f);
					return { file, url: URL.createObjectURL(file) };
				});
				// execCommand — for the native undo
				let html = items.map(x => `<img src="${x.url}" alt="${escapeHtml(x.file.name)}" class="uploading">`).join('');
				document.execCommand('insertHTML', false, html);
				for (let x of items)
					this.upload(x);
			},
			async upload(x) {
				let src = null;
				try {
					src = await hook(this, 'UploadImage', x.file);
				} catch (err) {
					console.error(err);
				}
				let url = src ? this.imageUrl(src) : null;
				if (src && !url)
					console.error(`a2-text-editor: ImageUrl knows nothing of '${src}' returned by UploadImage`);
				// erased while uploading — nothing to do
				let img = Array.from(this.$el.querySelectorAll('img')).find(i => i.getAttribute('src') === x.url);
				if (img) {
					if (url) {
						img.setAttribute('data-src', src);
						img.src = url;
						img.classList.remove('uploading');
					} else
						img.remove();
				}
				URL.revokeObjectURL(x.url);
				// an attribute change fires no input
				this.sync();
			}
		},
		mounted() {
			// not reactive: own state of the DOM, never rendered
			this.md = '';
			this.moving = false;
			this.gone = false;
			this.ranges = [];
			this.render(this.value);
		},
		destroyed() {
			this.gone = true;
			this.highlight();
		}
	});

	Vue.component('a2-text-view', {
		template: `<div class="a2-text-view" v-html="html" @load.capture="loaded" @error.capture="loaded"></div>`,
		// shown, not written — value, as a2-blob-preview
		props: {
			value: String,
			delegates: Object
		},
		computed: {
			html() {
				return toHtml(this.value || '', src => hook(this, 'ImageUrl', src) || null, true);
			}
		},
		methods: {
			loaded
		}
	});
})();

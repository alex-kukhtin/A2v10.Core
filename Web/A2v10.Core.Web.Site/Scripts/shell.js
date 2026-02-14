// Copyright © 2023-2026 Oleksandr Kukhtin. All rights reserved.

/*20260214-8622*/

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
				let li = getClosestLi(ev)
				if (!li) return;
				ev.target.setPointerCapture(ev.pointerId);
				let cr = li.getBoundingClientRect();
				this.movedTab = { tab: t, x: ev.x - cr.left, l: cr.left, pos: 0 };
				this._dragContext = { tabs: [], currentTab: -1, pointTab: -1, left: 0 };
			},
			pointerUp(ev, t) {
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
				s.host = `${window.location.protocol}//${window.location.host}`;
				let at = this.activeTab;
				if (!at) return false;
				s.url = at.url;
				s.query = at.query || '';
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
					let s = window.location.search;
					let p = window.location.pathname;
					window.history.replaceState(undefined, undefined, '/');
					this.navigateUrl(p, s);
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

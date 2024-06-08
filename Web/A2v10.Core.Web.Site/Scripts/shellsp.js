// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

/*20240608-8301*/

/* shellsp.js */

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

	const singlePageView = {
		props: {
			url: String
		},
		data() {
			return {
				needReload: false
			};
		},
		render(h) {
			return h('div', {
				attrs: {
					class: 'sp-content-view ' + this.cssClass
				}
			}, [h('include', {
				props: {
					src: this.currentView,
					needReload: this.needReload
				}
			})]);
		},
		computed: {
			cssClass() {
				return '';
			},
			currentView() {
				if (!this.url)
					return '';
				let root = window.$$rootUrl;
				return urlTools.combine(root, '/_page', this.url);
			}
		},
		methods: {
			_requery() {
				this.needReload = true;
				Vue.nextTick(() => this.needReload = false);
			}
		},
		created() {
			eventBus.$on('requery', this._requery);

		}
	};

	app.components["std:shellSinglePage"] = Vue.extend({
		store,
		data() {
			return {
				modals: [],
				modalRequeryUrl: '',
				traceEnabled: log.traceEnabled(),
				debugShowTrace: false,
				debugShowModel: false,
				dataCounter: 0,
				sidePaneUrl: '',
				navigatingUrl: '',
				lockRoute: false,
				requestsCount: 0,
				contextTabKey: 0,
				newVersionAvailable: false,
				currentPage: '',
				initUrl: ''
			};
		},
		components: {
			'a2-modal': modalComponent,
			'a2-toastr': toastrComponent,
			'a2-singlepage-view': singlePageView
		},
		computed: {
			modelStack() { return this.__dataStack__; },
			hasModals() { return this.modals.length > 0; },
			sidePaneVisible() { return !!this.sidePaneUrl; },
			processing() { return !this.hasModals && this.requestsCount > 0; }
		},
		methods: {
			navigate(to) {
				this.currentPage = to.url;
			},
			navigateUrl(url) {
				this.navigatingUrl = url;
				this.navigate({ url: url, title: '' });
			},
			navigateTo(to) {
				this.navigatingUrl = to.url;
				this.navigate({ url: to.url, title: '' });
			},
			dummy() {},
			reloadApplication() {
				window.location.reload();
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
				if (vm && vm.__requery)
					vm.__requery();
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
			setDocTitle(title) {
				document.title = title;
			},
			_pageReloaded(path) {
			},
			_eventToParentTab(ev) {
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
			},
			_closePlain() {
				this.currentPage = this.initUrl;
			}
		},
		watch: {
			traceEnabled(val) {
				log.enableTrace(val);
			}
		},
		mounted() {
			this.initUrl = this.$el.getAttribute('data-init-url');
			popup.registerPopup(this.$el);
			this.$el._close = this.__clickOutside;
			this.currentPage = this.initUrl;
		},
		created() {
			this.__dataStack__ = [];
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
			eventBus.$on('pageReloaded', this._pageReloaded);
			eventBus.$on('closePlain', this._closePlain);
			eventBus.$on('beginRequest', () => {
				this.requestsCount += 1;
				window.__requestsCount__ = this.requestsCount;
			});
			eventBus.$on('endRequest', () => {
				this.requestsCount -= 1;
				window.__requestsCount__ = this.requestsCount;
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

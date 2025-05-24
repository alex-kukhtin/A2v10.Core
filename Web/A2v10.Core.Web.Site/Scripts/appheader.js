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
			}
		},
		mounted() {
		}
	});
})();
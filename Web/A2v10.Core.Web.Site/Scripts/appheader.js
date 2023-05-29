// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

/*20230525-8100*/
/* tabbled:appheader.js */
(function () {

	const locale = window.$$locale;
	const http = require("std:http");

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
		}
	});
})();
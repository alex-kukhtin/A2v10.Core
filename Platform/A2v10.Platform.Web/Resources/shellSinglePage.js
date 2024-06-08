// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

/*20240608-8301*/

(function () {

	const menu = $(Menu);

	const Shell = component('std:shellSinglePage')

	const sp = menu.SysParams || {};

	const elem = new Shell({
		el: '#shell',
		data: {
			version: '$(AppVersion)',
			title: sp.AppTitle || '',
			subtitle: sp.AppSubTitle || '',
			userState: menu.UserState,
			isDebug: $(Debug),
			appData: $(AppData)
		}
	});

	window.$$rootUrl = '';
	window.$$debug = $(Debug);
})();
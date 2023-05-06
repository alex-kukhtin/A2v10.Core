
/*20230506-8086*/

(function () {

	const menu = $(Menu);

	const Shell = component('std:shellPlain')

	const sp = menu.SysParams || {};

	const elem = new Shell({
		el: '#shell',
		data: {
			version: '$(AppVersion)',
			menu: menu.Menu ? menu.Menu[0].Menu : null,
			title: sp.AppTitle || '',
			subtitle: sp.AppSubTitle || '',
			userState: menu.UserState,
			isDebug: $(Debug),
			appData: $(AppData),
		}
	});

	window.$$rootUrl = '';
	window.$$debug = $(Debug);
})();
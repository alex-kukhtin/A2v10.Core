// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Platform.Web;

// /_shell/script on Vue 3; Vue 2's is shellPlain.js. Same macros; the shell bundle creates the document's one app
// (a2v10.shell.start), the globals are set before it. A constant, not a Resource.resx entry: Vue 2's files stay as they are.
internal static class ShellScriptsVue3
{
	internal const String Plain = """
		(function () {

			const menu = $(Menu);

			const sp = menu.SysParams || {};

			window.$$rootUrl = '';
			window.$$debug = $(Debug);
			window.$$theme = '$(Theme)';

			a2v10.shell.start('#shell', {
				version: '$(AppVersion)',
				menu: menu.Menu ? menu.Menu[0].Menu : null,
				columns: menu.Columns || 1,
				title: sp.AppTitle || '',
				subtitle: sp.AppSubTitle || '',
				userState: menu.UserState,
				isDebug: $(Debug),
				appData: $(AppData),
			});
		})();
		""";
}

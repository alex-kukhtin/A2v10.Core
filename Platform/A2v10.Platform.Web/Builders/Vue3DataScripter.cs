// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

// A page's script on Vue 3: the body of a function the client's include runs (new Function), returning the page's
// descriptor; it mounts nothing and registers no page (A2v10.Vue3.Core, CORE-VUE3.md §4). The model function is
// Vue 2's, taken whole from the base (DataScript); only the frame around it differs. IDataScripter is implemented
// again so that GetModelScript is this one: VueDataScripter stays as it is.
public class Vue3DataScripter(
	IApplicationHost host, IAppCodeProvider codeProvider, ILocalizer localizer,
	ICurrentUser currentUser) : VueDataScripter(host, codeProvider, localizer, currentUser), IDataScripter
{
	// No IIFE: the client's function is the scope. uPeriod: modelData names it for a period property.
	const String HEADER = """
		<script type="text/javascript">
		'use strict';
		const eventBus = require('std:eventBus');
		const utils = require('std:utils');
		const uPeriod = require('std:period');
		const currentModule = $(CurrentModule);

		""";

	// createModel: a new root per call; include calls it once per mount
	const String FOOTER = """

		return {
			baseUrl: '$(BaseUrl)',
			inDialog: $(IsDialog),
			isIndex: $(IsIndex),
			isSkipDataStack: $(IsSkipDataStack),
			createModel: () => currentModule().dataModel
		};
		//# sourceURL=a2v10://$(BaseUrl)
		</script>
		""";

	public new async Task<ScriptInfo> GetModelScript(ModelScriptInfo msi)
	{
		var vue2 = await base.GetModelScript(msi);
		// the macros are replaced in the footer alone: the model function carries the application's text and data
		var footer = new StringBuilder(FOOTER)
			.Replace("$(BaseUrl)", msi.BaseUrl)
			.Replace("$(IsDialog)", msi.IsDialog.ToString().ToLowerInvariant())
			.Replace("$(IsIndex)", msi.IsIndex.ToString().ToLowerInvariant())
			.Replace("$(IsSkipDataStack)", msi.IsSkipDataStack.ToString().ToLowerInvariant());
		var script = new StringBuilder(HEADER)
			.Replace("$(CurrentModule)", vue2.DataScript)
			.Append(footer);
		return new ScriptInfo(Script: script.ToString(), DataScript: vue2.DataScript);
	}
}

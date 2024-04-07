// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Platform.Web;

static internal class SCRIPT_PARTS
{
	internal const String HEADER =
	@"
<script type=""text/javascript"">
'use strict';
(function() {
	const DataModelController = component('baseController');
	const eventBus = require('std:eventBus');
	const utils = require('std:utils');
	const uPeriod = require('std:period');
	const currentModule = $(CurrentModule);
";

	internal const String DATAFUNC =
	@"
function() {
	$(RequiredModules)

	const rawData = $(DataModelText);
	const template = $(TemplateText);

	$(ModelScript)
		
	return {
		dataModel: modelData(template, rawData)
	};
}
";

	internal const String DATAFUNC_SERVER =
	@"
	const $$server = function() {
		$(RequiredModules)

		const rawData = $(DataModelText);
		const rawDataRq = $(RawDataText);

		const template = $(TemplateText);

		$(ModelScript)
		
		const host = {
			$viewModel: {},
			$ctrl: {}
		};

		return {
			dataModelDb: function() {
				let md = modelData(template, rawData);
				md._host_ = host;
				return md;
			},
			dataModelRq: function() {
				let md = modelData(template, rawDataRq);
				md._host_ = host;
				return md;
			},
			createModel: function(jsonData) {
				let md = modelData(template, jsonData);
				md._host_ = host;
				return md;
			}
		};
	}
";

	internal const String FOOTER =
	@"
eventBus.$emit('beginLoad');
const vm = new DataModelController({
	el:'#$(RootId)',
	props: {
		inDialog: {type: Boolean, default: $(IsDialog)},
        isIndex: {type: Boolean, default: $(IsIndex)},
		isSkipDataStack: {type: Boolean, default: $(IsSkipDataStack)},
		pageTitle: {type: String}
	},
	data: currentModule().dataModel,
	computed: {
		utils() { return utils; },
		period() { return uPeriod; }
	},
	mounted() {
		eventBus.$emit('endLoad');
	}
});

vm.$data._host_ = {
	$viewModel: vm,
	$ctrl: vm.__createController__(vm)
};

vm.__doInit__('$(BaseUrl)');

})();
</script>
";
}

public class VueDataScripter(IApplicationHost host, IAppCodeProvider codeProvider, ILocalizer localizer, ICurrentUser currentUser) : IDataScripter
{

	private readonly IAppCodeProvider _codeProvider = codeProvider ?? throw new ArgumentNullException(nameof(codeProvider));
	private readonly ILocalizer _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
	private readonly IApplicationHost _host = host ?? throw new ArgumentNullException(nameof(host));
	private readonly ICurrentUser _currentUser = currentUser;

	public String CreateDataModelScript(IDataModel? model, Boolean isPlain)
	{
		if (model == null)
			return CreateEmptyStript();
		if (isPlain)
			return CreatePlainScript();
		return model.CreateScript(this);
	}

	public String CreateScript(IDataHelper helper, IDictionary<String, Object?>? sys, IDictionary<String, IDataMetadata> meta)
	{
		var sb = new StringBuilder();
		sb.AppendLine("function modelData(template, data) {");
		sb.AppendLine("const cmn = require('std:datamodel');");
		if (meta != null)
			sb.Append(GetConstructors(meta));
		sb.AppendLine("cmn.implementRoot(TRoot, template, ctors);");
		sb.AppendLine("let root = new TRoot(data);");
		sb.Append(SetModelInfo(helper, sys));
		sb.AppendLine("return root;}");
		return sb.ToString();
	}

	static String CreatePlainScript()
	{
		// as empty 
		return @"
function modelData(template, data) {
	const cmn = require('std:datamodel');
	function TRoot(source, path, parent) { cmn.createObject(this, source, path, parent);}
	cmn.defineObject(TRoot, { props: { } }, false);
	cmn.implementRoot(TRoot, template, {TRoot});
	let root = new TRoot(data);
	cmn.setModelInfo(root, {}, rawData); 
	if (template.loaded)
		template.loaded(rawData);
	return root;
}
";
	}

	static String CreateEmptyStript()
	{
		return @"
function modelData(template, data) {
	const cmn = require('std:datamodel');
	function TRoot(source, path, parent) { cmn.createObject(this, source, path, parent);}
	cmn.defineObject(TRoot, { props: { } }, false);
	cmn.implementRoot(TRoot, template, {TRoot});
	let root = new TRoot(data);
	cmn.setModelInfo(root, {}, rawData); 
	return root;
}
";
	}

	static String SetModelInfo(IDataHelper helper, IDictionary<String, Object?>? sys)
	{
		if (sys == null)
			return String.Empty;
		var list = new List<String>();
		foreach (var k in sys)
		{
			var val = k.Value;
			if (val is Boolean bVal)
				val = bVal ? "true" : "false";
			else if (val is String)
				val = $"'{val}'";
			else if (val is DateTime dateTimeObj)
				val = helper.DateTime2StringWrap(dateTimeObj);
			else if (val is Object valObj)
				val = JsonConvert.SerializeObject(valObj);
			list.Add($"'{k.Key}': {val}");
		}
		return $"cmn.setModelInfo(root, {list.ToJsonObject()}, rawData);";
	}

	static String GetConstructors(IDictionary<String, IDataMetadata> meta)
	{
		if (meta == null)
			return String.Empty;
		var sb = new StringBuilder();
		foreach (var m in meta)
		{
			sb.Append(GetOneConstructor(m.Key, m.Value));
			sb.AppendLine();
		}
		// make ctors
		var list = new List<String>();
		foreach (var re in meta)
		{
			list.Add(re.Key);
			if (re.Value.IsArrayType)
				list.Add($"{re.Key}Array");
		}
		sb.AppendLine($"const ctors = {list.ToJsonObject()};");
		return sb.ToString();
	}

	static StringBuilder GetOneConstructor(String name, IDataMetadata ctor)
	{
		var sb = new StringBuilder();
		String arrItem = ctor.IsArrayType ? "true" : "false";

		sb.AppendLine($"function {name}(source, path, parent) {{")
		.AppendLine("cmn.createObject(this, source, path, parent);")
		.AppendLine("}")
		// metadata
		.Append($"cmn.defineObject({name}, {{props: {{")
		.Append(GetProperties(ctor))
		.Append('}')
		.Append(GetSpecialProperties(ctor))
		.AppendLine($"}}, {arrItem});");

		if (ctor.IsArrayType)
		{
			sb.AppendLine($"function {name}Array(source, path, parent) {{")
			.AppendLine($"return cmn.createArray(source, path, {name}, {name}Array, parent);")
			.AppendLine("}");
		}
		return sb;
	}

	public static StringBuilder GetProperties(IDataMetadata meta)
	{
		var sb = new StringBuilder();
		foreach (var fd in meta.Fields)
		{
			var fm = fd.Value;
			String propObj = fm.GetObjectType($"{meta.Name}.{fd.Key}");
			if (propObj == "String")
			{
				if (fm.IsJson)
					propObj = $"{{type:String, len:{fm.Length}, json:true}}";
				else
					propObj = $"{{type:String, len:{fm.Length}}}";
			}
			else if (propObj == "TPeriod")
				propObj = $"{{type: uPeriod.constructor}}";
			sb.Append($"'{fd.Key}'")
			.Append(':')
			.Append(propObj)
			.Append(',');
		}
		if (sb.Length == 0)
			return sb;
		sb.RemoveTailComma();
		return sb;
	}

	static String GetCrossProperties(IDataMetadata meta)
	{
		var sb = new StringBuilder("{");
		foreach (var c in meta.Cross!)
		{
			sb.Append($"{c.Key}: [");
			if (c.Value != null)
				sb.Append(String.Join(",", c.Value.Select(s => $"'{s}'")));
			sb.Append("],");
		}
		sb.RemoveTailComma();
		sb.AppendLine("}");
		return sb.ToString();
	}

	static public String GetSpecialProperties(IDataMetadata meta)
	{
		var sb = new StringBuilder();
		if (!String.IsNullOrEmpty(meta.Id))
			sb.Append($"$id: '{meta.Id}',");
		if (!String.IsNullOrEmpty(meta.Name))
			sb.Append($"$name: '{meta.Name}',");
		if (!String.IsNullOrEmpty(meta.RowNumber))
			sb.Append($"$rowNo: '{meta.RowNumber}',");
		if (!String.IsNullOrEmpty(meta.HasChildren))
			sb.Append($"$hasChildren: '{meta.HasChildren}',");
		if (!String.IsNullOrEmpty(meta.MapItemType))
			sb.Append($"$itemType: {meta.MapItemType},");
		if (!String.IsNullOrEmpty(meta.Permissions))
			sb.Append($"$permissions: '{meta.Permissions}',");
		if (!String.IsNullOrEmpty(meta.Items))
			sb.Append($"$items: '{meta.Items}',");
		if (!String.IsNullOrEmpty(meta.Expanded))
			sb.Append($"$expanded: '{meta.Expanded}',");
		if (!String.IsNullOrEmpty(meta.MainObject))
			sb.Append($"$main: '{meta.MainObject}',");
		if (!String.IsNullOrEmpty(meta.Token))
			sb.Append($"$token: '{meta.Token}',");
		if (meta.IsGroup)
			sb.Append($"$group: true,");
		if (meta.HasCross)
			sb.Append($"$cross: {GetCrossProperties(meta)},");
		var lazyFields = new StringBuilder();
		foreach (var f in meta.Fields)
		{
			if (f.Value.IsLazy)
				lazyFields.Append($"'{f.Key}',");
		}
		if (lazyFields.Length != 0)
		{
			lazyFields.RemoveTailComma();
			sb.Append($"$lazy: [{lazyFields}]");
		}
		if (sb.Length == 0)
			return String.Empty;
		sb.RemoveTailComma();
		return ", " + sb.ToString();
	}


	public String CreateServerScript(IDataModel model, String template)
	{
		throw new NotImplementedException(nameof(CreateServerScript));
	}

	public String CreateServerScript(IDataModel model, String template, String requiredModules)
	{
		var sb = new StringBuilder(SCRIPT_PARTS.DATAFUNC);
		sb.Replace("$(TemplateText)", template);
		sb.Replace("$(RequiredModules)", requiredModules);
		String modelScript = model.CreateScript(this);
		String rawData = JsonConvert.SerializeObject(model.Root, JsonHelpers.StandardSerializerSettings);
		sb.Replace("$(DataModelText)", rawData);
		sb.Replace("$(ModelScript)", modelScript);

		return sb.ToString();
	}

	static String CreateTemplateForWrite(String? fileTemplateText)
	{
		if (fileTemplateText != null && fileTemplateText.Contains("define([\"require\", \"exports\"]"))
		{
			// amd module
			return fileTemplateText;
		}

		const String tmlHeader =
@"(function() {
	let module = { exports: undefined };
	(function(module, exports) {
";

		const String tmlFooter =
@"
	})(module, module.exports);
	return module.exports;
})()";

		var sb = new StringBuilder();

		sb.AppendLine()
		.AppendLine(tmlHeader)
		.AppendLine(fileTemplateText)
		.AppendLine(tmlFooter);
		return sb.ToString();
	}

	void AddRequiredModules(StringBuilder sb, String clientScript)
	{
		const String tmlHeader =
@"
	app.modules['$(Module)'] = function() {
	let module = { exports: undefined };
	(function(module, exports) {
";

		const String tmlFooter =
@"
	})(module, module.exports);
	return module.exports;
};";

		if (String.IsNullOrEmpty(clientScript))
			return;
		var _modulesWritten = new HashSet<String>();
		Int32 iIndex = 0;
		while (true)
		{
			String? moduleName = FindModuleNameFromString(clientScript, ref iIndex);
			if (moduleName == null)
				return; // not found
			if (String.IsNullOrEmpty(moduleName))
				continue;
			if (moduleName.StartsWith("global/", StringComparison.InvariantCultureIgnoreCase))
				continue;
			if (moduleName.StartsWith("std:", StringComparison.InvariantCultureIgnoreCase))
				continue;
			if (moduleName.StartsWith("app:", StringComparison.InvariantCultureIgnoreCase))
				continue;
			if (_modulesWritten.Contains(moduleName))
				continue;
			var fileName = moduleName.RemoveHeadSlash().AddExtension("js");

			using var stream = _codeProvider.FileStreamRO(fileName)
				?? throw new InvalidOperationException($"File not found '{fileName}'");
			using var rdr = new StreamReader(stream);
			String moduleText = rdr.ReadToEnd();

			if (moduleText.Contains("define([\"require\", \"exports\"]"))
			{
				sb.Append($"if (app.modules['{moduleName}'] == undefined) {{")
				.AppendLine()
				.Append($"app.modules['{moduleName}'] = function() {{return ")
				.AppendLine(Localize(moduleText))
				.AppendLine()
				.AppendLine("}};");
			}
			else
			{
				sb.AppendLine(tmlHeader.Replace("$(Module)", moduleName))
					.AppendLine(Localize(moduleText))
					.AppendLine(tmlFooter)
					.AppendLine();
			}
			_modulesWritten.Add(moduleName);
			AddRequiredModules(sb, moduleText);
		}
	}

	public static String? FindModuleNameFromString(String text, ref Int32 pos)
	{
		String funcName = "require";
		Int32 rPos = text.IndexOf(funcName, pos);
		if (rPos == -1)
			return null; // we do not continue, we did not find anything
		pos = rPos + funcName.Length;
		// check that we are not in the comment
		Int32 oc = text.LastIndexOf("/*", rPos);
		Int32 cc = text.LastIndexOf("*/", rPos);
		if (oc != -1)
		{
			// there is an opening comment
			if (cc == -1)
			{
				return String.Empty; // нет закрывающего
			}
			if (cc < oc)
			{
				return String.Empty; // закрывающий левее открывающего, мы внутри
			}
		}
		Int32 startLine = text.LastIndexOfAny(['\r', '\n'], rPos);
		oc = text.LastIndexOf("//", rPos);
		if ((oc != 1) && (oc > startLine))
			return String.Empty; // есть однострочный и он после начала строки

		Tokenizer? tokenizer = null;
		try
		{
			// проверим точку, как предыдущий токен
			var dotPos = text.LastIndexOf('.', rPos);
			if (dotPos != -1)
			{
				tokenizer = new Tokenizer(text, dotPos);
				if (tokenizer.token.id == Tokenizer.TokenId.Dot)
				{
					tokenizer.NextToken();
					var tok = tokenizer.token;
					if (tok.id == Tokenizer.TokenId.Identifier && tok.Text == "require")
					{
						tokenizer.NextToken();
						if (tokenizer.token.id == Tokenizer.TokenId.OpenParen)
							return String.Empty; /* есть точка перед require */
					}
				}
			}
			tokenizer = new Tokenizer(text, rPos + funcName.Length);
			if (tokenizer.token.id == Tokenizer.TokenId.OpenParen)
			{
				tokenizer.NextToken();
				if (tokenizer.token.id == Tokenizer.TokenId.StringLiteral)
				{
					pos = tokenizer.GetTextPos();
					return tokenizer.token.UnquotedText.Replace("\\\\", "/");
				}
			}
			pos = tokenizer.GetTextPos();
			return String.Empty;
		}
		catch (ParseError /*ex*/)
		{
			// parser error
			if (tokenizer != null)
				pos = tokenizer.GetTextPos();
			return null;
		}
	}

	private String? Localize(String? source)
	{
		String? result = _localizer.Localize(null, source, replaceNewLine: false);
		return _host.GetAppSettings(result);
	}

	// TODO: Add To Interface
	public async Task<String> GetTemplateScript(IModelView view)
	{
		var sbRequired = new StringBuilder();
		if (view.Path == null)
			throw new InvalidOperationException("Model.Path is null");
		var pathToRead = _codeProvider.MakePath(view.Path, $"{view.Template}.js");
		using var stream = _codeProvider.FileStreamRO(pathToRead)
			?? throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
		using var sr = new StreamReader(stream);
		var fileTemplateText = await sr.ReadToEndAsync() ??
			throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
		AddRequiredModules(sbRequired, fileTemplateText);
		return CreateTemplateForWrite(Localize(fileTemplateText));
	}

	public async Task<ScriptInfo> GetModelScript(ModelScriptInfo msi)
	{
		var output = new StringBuilder();
		String dataModelText = "{}";
		String templateText = "{}";
		var sbRequired = new StringBuilder();

		// write model script
		String? fileTemplateText;
		if (msi.Template != null)
		{
			if (msi.Path == "@Model.Template")
				fileTemplateText = msi.Template;
			else
			{
				if (msi.Path == null)
					throw new InvalidOperationException("Model.Path is null");
				var pathToRead = _codeProvider.MakePath(msi.Path, $"{msi.Template}.js");
				using var stream = _codeProvider.FileStreamRO(pathToRead)
					?? throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
				using var sr = new StreamReader(stream);
				fileTemplateText = await sr.ReadToEndAsync() ??
					throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
			}
			AddRequiredModules(sbRequired, fileTemplateText);
			templateText = CreateTemplateForWrite(Localize(fileTemplateText));
		}
		if (msi.DataModel != null)
		{
			dataModelText = JsonConvert.SerializeObject(msi.DataModel.Root,
				JsonHelpers.ConfigSerializerSettings(_host.IsDebugConfiguration));
		}

		var header = new StringBuilder(SCRIPT_PARTS.HEADER);
		header.Replace("$(RootId)", msi.RootId);

		var modelFunc = new StringBuilder(SCRIPT_PARTS.DATAFUNC);
		modelFunc.Replace("$(RequiredModules)", sbRequired?.ToString());
		modelFunc.Replace("$(TemplateText)", Localize(templateText));
		modelFunc.Replace("$(DataModelText)", dataModelText);
		String modelScript = CreateDataModelScript(msi.DataModel, msi.IsPlain);
		modelFunc.Replace("$(ModelScript)", modelScript);

		header.Replace("$(CurrentModule)", modelFunc.ToString());
		output.Append(header);

		var footer = new StringBuilder(SCRIPT_PARTS.FOOTER)
		.Replace("$(RootId)", msi.RootId)
		.Replace("$(BaseUrl)", msi.BaseUrl)
		.Replace("$(IsDialog)", msi.IsDialog.ToString().ToLowerInvariant())
		.Replace("$(IsIndex)", msi.IsIndex.ToString().ToLowerInvariant())
		.Replace("$(IsSkipDataStack)", msi.IsSkipDataStack.ToString().ToLowerInvariant());
		output.Append(footer);

		return new ScriptInfo(
			Script: output.ToString(),
			DataScript: modelFunc.ToString()
			);
	}

	public ScriptInfo GetServerScript(ModelScriptInfo msi)
	{
		throw new NotImplementedException();
	}

	public async Task<ScriptInfo> GetServerScriptAsync(ModelScriptInfo msi)
	{
		StringBuilder? sbRequired = null;
		String templateText = "{}";
		if (msi.Path == null)
			throw new InvalidProgramException("ModelScriptInfo.Path is null");
		if (msi.Template != null)
		{
			var fileTemplatePath = _codeProvider.MakePath(msi.Path, msi.Template + ".js");
			using Stream stream = _codeProvider.FileStreamRO(fileTemplatePath)
				?? throw new FileNotFoundException($"File not found. '{fileTemplatePath}'");
			using var sr = new StreamReader(stream);
			var fileTemplateText = await sr.ReadToEndAsync();
			sbRequired = new StringBuilder();
			AddRequiredModules(sbRequired, fileTemplateText);
			templateText = CreateTemplateForWrite(Localize(fileTemplateText));
		}
		var sb = new StringBuilder(SCRIPT_PARTS.DATAFUNC_SERVER);
		sb.Replace("$(TemplateText)", templateText);
		sb.Replace("$(RequiredModules)", sbRequired?.ToString());
		String? modelScript = msi.DataModel?.CreateScript(this);
		String? rawData = msi.DataModel != null ? JsonConvert.SerializeObject(msi.DataModel.Root,
			JsonHelpers.ConfigSerializerSettings(_host.IsDebugConfiguration))
			: null;
		sb.Replace("$(DataModelText)", rawData);
		sb.Replace("$(RawDataText)", msi.RawData ?? "{}");
		sb.Replace("$(ModelScript)", modelScript);

		return new ScriptInfo
		(
			Script: sb.ToString(),
			DataScript: null
		);
	}
}

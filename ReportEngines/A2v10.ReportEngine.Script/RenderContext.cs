// Copyright © 2022-2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Jint.Native;

using A2v10.Infrastructure;
using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Script;

public enum ResolveResultType
{
	Default,
	QrCode
}
public record ResolveResult(String? Value, Byte[]? Stream = null, ResolveResultType ResultType = ResolveResultType.Default);
public partial class RenderContext
{
	private readonly IReportLocalizer _localizer;

	private readonly CultureInfo _formatProvider;
	private readonly String _templatePath;
	private readonly ConcurrentDictionary<String, JsValue> _accessFuncs = [];

	public RenderContext(String templatePath, IReportLocalizer localizer, ExpandoObject model, String? code)
	{
		_localizer = localizer;
        // TODO: replace _templatePath to _appCodeReader;
        _templatePath = templatePath;
		DataModel = model;
		var  clone = _localizer.CurrentCulture.Clone();
		if (clone is CultureInfo cloneCI && cloneCI != null)
			_formatProvider = cloneCI;
		else
			throw new InvalidOperationException("Invalid Current culture");
		_formatProvider.NumberFormat.CurrencyGroupSeparator = "\u00A0";
		_formatProvider.NumberFormat.NumberGroupSeparator = "\u00A0";

		Engine = new ScriptEngine(model, _formatProvider, code);
	}

	public ScriptEngine Engine { get; }
	public ExpandoObject DataModel { get; }

	public String ValueToString(Object? value, DataType dataType = DataType.String, String? format = null)
	{
		if (value == null)
			return String.Empty;

		if (!String.IsNullOrEmpty(format) && format != "General")
			return String.Format(_formatProvider, $"{{0:{format}}}", value);

		var result = dataType switch
		{
			DataType.Currency => String.Format(_formatProvider, "{0:#,##0.00##}", value),
			DataType.Number => String.Format(_formatProvider, "{0:#,##0.########}", value),
			DataType.Time => String.Format(_formatProvider, "{0:T}", value),
			DataType.Date => String.Format(_formatProvider, "{0:d}", value),
			DataType.DateTime => String.Format(_formatProvider, "{0:g}", value),
			_ => _localizer.Localize(value.ToString()) ?? String.Empty,
		};
		result = result.Replace("\\n", "\n");
		return result;
	}

	public Byte[]? GetFileAsByteArray(String? fileName)
	{
		if (String.IsNullOrEmpty(fileName))
			return null;
		if (Path.IsPathRooted(fileName))
			throw new InvalidDataException("Invalid path. The path must be relative");
		var templDir = Path.GetDirectoryName(_templatePath) ?? String.Empty;
		var fullPath = Path.GetFullPath(Path.Combine(templDir, fileName));
		return File.ReadAllBytes(fullPath);
	}

	public Byte[]? GetValueAsByteArray(Object value, ExpandoObject scope, String propertyName)
	{
		if (value == null)
			return null;
		if (value is not XamlElement xamlElem)
			return null;
		var bindRuntime = xamlElem.GetBindRuntime(propertyName);
		if (bindRuntime == null || bindRuntime.Expression == null)
			return null;
		var lastDot = bindRuntime.Expression.LastIndexOf('.');
		if (lastDot == -1)
			return null;
		var objVal = Evaluate(bindRuntime.Expression[..lastDot], scope);
		if (objVal == null || objVal is not ExpandoObject eoVal)
			return null;
		return eoVal.Get<Byte[]>(bindRuntime.Expression[(lastDot + 1)..]);
	}

	public String? GetValueAsString(Object value, ExpandoObject scope, String propertyName = "Content")
	{
		if (value == null)
			return null;
		if (value is String strElem)
			return _localizer.Localize(strElem);
		if (value is ContentElement contElem)
		{
			var contBind = contElem.GetBindRuntime("Content");
			if (contBind != null)
			{
				var val = Evaluate(contBind.Expression, scope);
				if (val != null)
					return ValueToString(val, contBind.DataType, contBind.Format);
			}
			else if (contElem.Content != null)
				return ValueToString(contElem.Content);
		}
		else if (value is XamlElement xamlElem)
		{
			var contBind = xamlElem.GetBindRuntime(propertyName);
			if (contBind != null)
			{
				var val = Evaluate(contBind.Expression, scope);
				if (val != null)
					return ValueToString(val, contBind.DataType, contBind.Format);
			}
		}
		return null;
	}

	private static DataType MatchDataType(DataType dt, Object? value)
	{
		if (dt != DataType.String)
			return dt;
		return value switch
		{
			Decimal => DataType.Currency,
			Single or Double => DataType.Number,
			DateTime => DataType.DateTime,
			_ => dt
		};
	}

	// Ключ — само выражение, поэтому кеш один на ячейки и биндинги
	private JsValue GetOrCreateAccessFunc(String expression)
	{
		if (_accessFuncs.TryGetValue(expression, out JsValue? func))
			return func;
		func = Engine.CreateAccessFunction(expression);
		_accessFuncs.TryAdd(expression, func);
		return func;
	}

	public Object? Evaluate(String? expression, ExpandoObject scope)
	{
		if (String.IsNullOrEmpty(expression))
			return null;
		return Engine.Invoke(GetOrCreateAccessFunc(expression!), scope);
	}

	public IList<ExpandoObject>? EvaluateCollection(String expression, ExpandoObject scope)
	{
		var list = Evaluate(expression, scope);
		if (list == null)
			return null;
		if (list is IList<ExpandoObject> listExp)
			return listExp;
		throw new InvalidOperationException($"'{expression}' is not a collection");
	}


	const String RESOLVE_PATTERN = "\\{(.+?)\\}";
#if NET7_0_OR_GREATER
	[GeneratedRegex(RESOLVE_PATTERN, RegexOptions.None, "en-US")]
	private static partial Regex ResolveRegex();
#else
	private static Regex RESOLVEREGEX => new(RESOLVE_PATTERN, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase );
	private static Regex ResolveRegex() => RESOLVEREGEX;
#endif

	// Двоеточие делит только путь: внутри выражения это тернарник
	private static String? ScopePath(String key, ref String? format)
	{
		if (ScriptEngine.IsScopePath(key))
			return key;
		var spos = key.IndexOf(':');
		if (spos == -1)
			return null;
		var path = key[..spos];
		if (!ScriptEngine.IsScopePath(path))
			return null;
		format = key[(spos + 1)..];
		return path;
	}

	public ResolveResult? Resolve(String? source, ExpandoObject scope, DataType dataType, String? format)
	{
		if (String.IsNullOrEmpty(source))
			return new ResolveResult(source);
		var ms = ResolveRegex().Matches(source);
		if (ms.Count == 0)
			return new ResolveResult(source);
		var sb = new StringBuilder(source);
		foreach (Match m in ms.Cast<Match>())
		{
			String? valResult = null;
			String key = m.Groups[1].Value;
			// Ветку выбирает форма записи, а не скобки: {a.b} — путь, {f(x)} — выражение
			var path = ScopePath(key, ref format);
			if (path != null)
			{
				var valObj = scope.Eval<Object>(path);
				if (valObj is Byte[] bytes)
					return new ResolveResult(null, bytes);
				valResult = ValueToString(valObj, MatchDataType(dataType, valObj), format);
			}
			else
			{
				var valObj = Evaluate(key, scope);
				if (valObj is QrCodeValue qrCodeValue)
					return new ResolveResult(qrCodeValue.Value, null, ResolveResultType.QrCode);
				valResult = ValueToString(valObj, MatchDataType(dataType, valObj), format);
			}
			if (ms.Count == 1 && m.Groups[0].Value == source)
				return new ResolveResult(valResult ?? String.Empty); // single element
			sb.Replace(m.Value, valResult);

		}
		return new ResolveResult(sb.ToString());
	}

	public String? ResolveModel(String? value)
	{
		if (value == null)
			return null;
		var sb = new StringBuilder(value);
		sb.Replace("{{", "{");
		sb.Replace("}}", "}");
		var rx =  Resolve(sb.ToString(), DataModel, DataType.String, null);
		// inner expressions!!!!
		return Resolve(rx?.Value, DataModel, DataType.String, null)?.Value;
	}

	public Boolean IsVisible(XamlElement elem, ExpandoObject scope)
	{
		var ifbind = elem.GetBindRuntime(nameof(elem.If));
		if (ifbind == null)
			return elem.If == null || elem.If.Value;

		var expression = ifbind.Expression;
		if (String.IsNullOrEmpty(expression))
			return true;
		// '!' про значение, а не про адрес: иначе !Done в строке ушло бы к корню
		var invert = expression!.StartsWith('!');
		if (invert)
			expression = expression[1..];
		var val = Evaluate(expression, scope);
		var visible = val switch
		{
			Boolean boolVal => boolVal,
			null => false,
			_ => true
		};
		return invert ? !visible : visible;
	}
}

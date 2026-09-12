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

// Растр и SVG рисуются разными вызовами, поэтому различие названо типом, а не флагом:
// слот картинки не может оказаться «и то, и другое» или «ни то, ни другое»
public abstract record ReportImage
{
	public sealed record Raster(Byte[] Bytes) : ReportImage;
	public sealed record Svg(String Text) : ReportImage;
}

public partial class RenderContext
{
	private readonly IReportLocalizer _localizer;

	private readonly CultureInfo _formatProvider;
	private readonly IAppCodeProvider _codeProvider;
	private readonly String _basePath;
	private readonly ConcurrentDictionary<String, JsValue> _accessFuncs = [];

	public RenderContext(IAppCodeProvider codeProvider, String basePath, IReportLocalizer localizer, ExpandoObject model, String? code)
	{
		_localizer = localizer;
		_codeProvider = codeProvider;
		// Папка отчёта в терминах провайдера, а не файловой системы: на маршрутах потока
		// и базы файлов нет, но имя внутри бланка всё равно ищется рядом с бланком
		_basePath = basePath;
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

	// Единственный вход для слотов, ждущих картинку. Значение либо байты (varbinary из
	// модели), либо строка — тогда это имя файла. Строка остаётся текстом везде, кроме
	// таких слотов: иначе каждая текстовая ячейка книги стала бы именем файла
	public ReportImage? ResolveImage(Object? value)
	{
		return value switch
		{
			Byte[] bytes => bytes.Length > 0 ? ImageFromBytes(bytes) : null,
			String fileName => String.IsNullOrEmpty(fileName) ? null : ImageFromBytes(ReadFile(fileName)),
			_ => null
		};
	}

	// Слот картинки в разметке: связанное выражение, иначе литерал
	public ReportImage? ResolveImage(XamlElement elem, ExpandoObject scope, String propertyName, Object? literal)
	{
		var bind = elem.GetBindRuntime(propertyName);
		return ResolveImage(bind != null ? Evaluate(bind.Expression, scope) : literal);
	}

	// Растр или SVG решают первые байты, а не расширение: у байтов из varbinary имени нет,
	// а имя файла может врать. Побочно рисуется SVG, лежащий в базе — раньше он молча пустой
	public static ReportImage ImageFromBytes(Byte[] bytes)
	{
		var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
		var sig = start;
		while (sig < bytes.Length && bytes[sig] <= 0x20)
			sig++;
		if (StartsWith(bytes, sig, "<svg") || StartsWith(bytes, sig, "<?xml"))
			return new ReportImage.Svg(Encoding.UTF8.GetString(bytes, start, bytes.Length - start));
		return new ReportImage.Raster(bytes);
	}

	private static Boolean StartsWith(Byte[] bytes, Int32 offset, String prefix)
	{
		if (offset + prefix.Length > bytes.Length)
			return false;
		for (var i = 0; i < prefix.Length; i++)
			if (bytes[offset + i] != (Byte)prefix[i])
				return false;
		return true;
	}

	// Всегда байты и всегда ресурсный канал: FileStreamRO у CLR-провайдера отдаёт
	// GetText через UTF8, то есть растр вернулся бы мусором и молча. Ни путь, ни промах
	// тут не проверяются — и то и другое забота провайдера, он один знает, что искал
	private Byte[] ReadFile(String fileName)
	{
		var path = _codeProvider.MakePath(_basePath, fileName);
		using var stream = _codeProvider.FileStreamResource(path);
		using var mem = new MemoryStream();
		stream.CopyTo(mem);
		return mem.ToArray();
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

	// Единственный вход для всего, что пишется в бланке. Путь отвечают данные, остальное — JS.
	// Путь в JS не заходит никогда: там Decimal становится Double, а Byte[] — массивом чисел
	public Object? Evaluate(String? text, ExpandoObject scope)
	{
		if (String.IsNullOrWhiteSpace(text))
			return null;
		// Пробелы по краям смысла не несут, но меняли ветку: " Name " — уже не путь, и уходило
		// в JS, где свободное имя читается от корня. В книге их ставят, не думая об этом
		var expr = text!.Trim();
		if (PathRegex().IsMatch(expr))
			return Walk(expr, scope);
		return Engine.Invoke(GetOrCreateAccessFunc(expr), scope);
	}

	// Root и this — два слова, с которых путь может начаться; без них он идёт от scope
	private Object? Walk(String path, ExpandoObject scope)
	{
		var dot = path.IndexOf('.');
		var head = dot == -1 ? path : path[..dot];
		ExpandoObject? start = head switch
		{
			"Root" => DataModel,
			"this" => scope,
			_ => null
		};
		if (start == null)
			return scope.Eval<Object>(path);
		return dot == -1 ? start : start.Eval<Object>(path[(dot + 1)..]);
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
	[GeneratedRegex(RESOLVE_PATTERN, RegexOptions.None, "en-US")]
	private static partial Regex ResolveRegex();

	// Путь — имена через точку, у имени может быть индекс: ровно то, что проходит обход
	const String PATH = @"[\p{L}_$][\w$]*(?:\[\d+\])?(?:\.[\p{L}_$][\w$]*(?:\[\d+\])?)*";

	[GeneratedRegex($"^{PATH}$")]
	private static partial Regex PathRegex();

	// Формат — хвост после пути. У тернарника слева от двоеточия не путь, он не совпадёт
	[GeneratedRegex($"^({PATH}):(.+)$", RegexOptions.Singleline)]
	private static partial Regex FormattedPathRegex();

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
			// Trim до разбора формата, иначе " Sum:#,##0.00 " не узнаётся как путь с форматом
			String key = m.Groups[1].Value.Trim();
			var keyFormat = format;
			var fm = FormattedPathRegex().Match(key);
			if (fm.Success)
			{
				key = fm.Groups[1].Value;
				keyFormat = fm.Groups[2].Value;
			}
			var valObj = Evaluate(key, scope);
			if (valObj is Byte[] bytes)
				return new ResolveResult(null, bytes);
			if (valObj is QrCodeValue qrCodeValue)
				return new ResolveResult(qrCodeValue.Value, null, ResolveResultType.QrCode);
			var valResult = ValueToString(valObj, MatchDataType(dataType, valObj), keyFormat);
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
		if (String.IsNullOrWhiteSpace(expression))
			return true;
		expression = expression!.Trim();
		// '!' снимается только с пути: путь проходится по данным, отрицать его там нечем, а
		// оставить его путём обязательно - иначе !Done в строке ушло бы искаться к корню.
		// Выражение отрицает себя само, в JS, и с правильным приоритетом: '!A && B' там не '!(A && B)'
		var invert = expression.StartsWith('!') && PathRegex().IsMatch(expression[1..].Trim());
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

// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Text.RegularExpressions;

using Jint;
using Jint.Native;

using A2v10.Infrastructure;

namespace A2v10.ReportEngine.Script;

public partial class ScriptEngine
{
	private readonly Engine _engine;
	private readonly CultureInfo _culture;

	public ScriptEngine(ExpandoObject model, CultureInfo cultureInfo, String? code)
	{
		_culture = cultureInfo;
		_engine = new Engine(opts =>
		{
			opts.Strict = true;
			//opts.Debugger.Enabled = true;
			opts.LocalTimeZone(TimeZoneInfo.Utc);
			opts.Culture(cultureInfo);
		});
		if (!String.IsNullOrEmpty(code))
			_engine.Evaluate(code!);

		// Свободное имя — от корня, так написаны боевые формы. Root — то же самое явно
		foreach (var item in model)
			if (item.Value != null)
				_engine.SetValue(item.Key, item.Value);

		_engine.SetValue("Root", model);

		_engine.SetValue("spellMoney", SpellMoney);
        _engine.SetValue("spellMoneyEn", SpellMoneyEn);
        _engine.SetValue("formatDate", FormatDate);
		_engine.SetValue("qrCode", QrCodeFunc);
	}

	// Обёртка — function, а не стрелка: у стрелки this лексический, и его пришлось бы
	// подставлять в текст, ломая thisYear и литералы. Скобки вокруг тела — от ASI
	public JsValue CreateAccessFunction(String expression)
	{
		var body = IsScopePath(expression) ? $"this.{expression}" : expression;
		return _engine.Evaluate($"(function() {{ return ({body}); }})");
	}

	// Один вопрос на два: ставить ли this. в выражении и пройдёт ли путь C#-обход по данным
	public static Boolean IsScopePath(String expression)
	{
		return BarePathRegex().IsMatch(expression)
			&& !StartsWith(expression, "Root") && !StartsWith(expression, "this");
	}

	// Root в модели нет, this называет сам scope — обход по данным не пройдёт ни то, ни другое
	private static Boolean StartsWith(String expression, String name)
	{
		return expression == name || expression.StartsWith($"{name}.", StringComparison.Ordinal);
	}

	const String BARE_PATH_PATTERN = @"^[A-Za-z_$][A-Za-z0-9_$]*(\.[A-Za-z_$][A-Za-z0-9_$]*)*$";
	[GeneratedRegex(BARE_PATH_PATTERN)]
	private static partial Regex BarePathRegex();

	public Object? Invoke(JsValue func, ExpandoObject scope)
	{
		return _engine.Invoke(func, scope, []).ToObject();
	}

	String SpellMoney(Object value, String currencyCode)
	{
		if (String.IsNullOrEmpty(currencyCode))
			currencyCode = "980";
		var d = Convert.ToDecimal(value);
		return SpellString.SpellCurrency(d, _culture, currencyCode);
	}

    String SpellMoneyEn(Object value, String currencyCode)
    {
        if (String.IsNullOrEmpty(currencyCode))
            currencyCode = "980";
        var d = Convert.ToDecimal(value);
        return SpellString.SpellCurrencyEn(d, currencyCode);
    }

    String FormatDate(Object value, String format)
	{
		if (value is DateTime valDate)
			return valDate.ToString(format, _culture);
		return "Invalid date";
	}

	Object QrCodeFunc(Object value)
	{
		return new QrCodeValue(value?.ToString() ?? String.Empty);
	}
}

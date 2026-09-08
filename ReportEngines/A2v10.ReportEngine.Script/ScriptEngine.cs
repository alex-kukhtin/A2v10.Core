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

		// Свободное имя читается от корня: так написаны боевые формы, поэтому хостинг остаётся.
		// Root даёт то же самое явно — единственный способ выйти из scope на любой глубине
		foreach (var item in model)
			if (item.Value != null)
				_engine.SetValue(item.Key, item.Value);

		_engine.SetValue("Root", model);

		_engine.SetValue("spellMoney", SpellMoney);
        _engine.SetValue("spellMoneyEn", SpellMoneyEn);
        _engine.SetValue("formatDate", FormatDate);
		_engine.SetValue("qrCode", QrCodeFunc);
	}

	// Голый путь читается от текущего scope, всё остальное — обычный JS, где this и есть scope.
	// Обёртка именно function, а не стрелка: у стрелки this лексический, связать ресивер нечем,
	// и его приходилось заменять в тексте — ценой идентификаторов вроде thisYear и литералов.
	// Выражение в скобках: без них перевод строки в начале съедает ASI после return.
	public JsValue CreateAccessFunction(String expression)
	{
		var body = IsScopePath(expression) ? $"this.{expression}" : expression;
		return _engine.Evaluate($"(function() {{ return ({body}); }})");
	}

	// Один вопрос отвечает на два: получит ли путь префикс this. внутри выражения и может ли
	// его пройти C#-ный Eval по scope. Поэтому правило одно и живёт здесь, а не в двух местах
	public static Boolean IsScopePath(String expression)
	{
		return BarePathRegex().IsMatch(expression)
			&& !StartsWith(expression, "Root") && !StartsWith(expression, "this");
	}

	// Root и this — единственные начала, которые C#-ный обход по данным пройти не может:
	// первого в модели нет, второе называет сам scope. Оба уходят в выражение
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

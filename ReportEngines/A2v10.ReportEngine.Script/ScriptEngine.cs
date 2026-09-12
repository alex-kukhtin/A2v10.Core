// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;

using Jint;
using Jint.Native;

using A2v10.Infrastructure;

namespace A2v10.ReportEngine.Script;

public class ScriptEngine
{
	private readonly Engine _engine;
	private readonly CultureInfo _culture;

	// Бюджет коробки. Власти у выражения нет (без AllowClr оно видит только модель), остались
	// ресурсы: без глубины рекурсия в Code — StackOverflowException, который не ловится и
	// роняет процесс целиком; без счётчика while(true) вешает поток запроса. Счётчик
	// детерминирован, таймер — страховка для того, что счётчик не видит (тяжёлый нативный вызов).
	// Цифры — с запасом на reduce по десяткам тысяч строк, а не на здравый смысл шаблона
	const Int32 MAX_RECURSION = 256;
	const Int32 MAX_STATEMENTS = 1_000_000;
	static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(5);

	public ScriptEngine(ExpandoObject model, CultureInfo cultureInfo, String? code)
	{
		_culture = cultureInfo;
		_engine = new Engine(opts =>
		{
			opts.Strict = true;
			//opts.Debugger.Enabled = true;
			opts.LocalTimeZone(TimeZoneInfo.Utc);
			opts.Culture(cultureInfo);
			opts.LimitRecursion(MAX_RECURSION);
			opts.MaxStatements(MAX_STATEMENTS);
			opts.TimeoutInterval(TIMEOUT);
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
		return _engine.Evaluate($"(function() {{ return ({expression}); }})");
	}

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

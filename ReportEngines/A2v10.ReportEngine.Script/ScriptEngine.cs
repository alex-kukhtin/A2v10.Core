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

		// all properties as Root objects
		foreach (var item in model)
			if (item.Value != null)
				_engine.SetValue(item.Key, item.Value);

		_engine.SetValue("spellMoney", SpellMoney);
        _engine.SetValue("spellMoneyEn", SpellMoneyEn);
        _engine.SetValue("formatDate", FormatDate);
		_engine.SetValue("qrCode", QrCodeFunc);
	}

	public IList<ExpandoObject> EvaluateCollection(String expression)
	{
		var list = _engine.Evaluate(expression).ToObject();
		if (list is IList<ExpandoObject> listExp)
			return listExp;
		throw new InvalidOperationException($"'{expression}' is not a collection");
	}
    public static IList<ExpandoObject>? GetCollection(ExpandoObject value, String expression)
    {
        var list = value.Eval<Object>(expression);
		if (list == null)
			return null;
        if (list is IList<ExpandoObject> listExp)
            return listExp;
        throw new InvalidOperationException($"'{expression}' is not a collection");
    }

    public Object? EvaluateValue(String? expression)
	{
		if (expression == null)
			return null;
		return _engine.Evaluate(expression).ToObject();
	}

	public JsValue CreateAccessFunction(String expression)
	{
		var exp = $"_elem_ => _elem_.{expression}";
		if (expression.Contains('(')) // call function
			exp = $"_elem_ => {expression.Replace("this", "_elem_").Replace("Root.", "")}";
		else if (expression.StartsWith("Root."))
			exp = $"_elem_ => {expression.Replace("Root.", "")}";
		return _engine.Evaluate(exp);
	}

	public Object? Invoke(JsValue func, ExpandoObject? data, String? expression)
	{
		if (data != null)
			return _engine.Invoke(func, data).ToObject();
		else if (!String.IsNullOrEmpty(expression))
			return _engine.Evaluate(expression!)?.ToObject();
		return null;
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

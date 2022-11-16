// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Globalization;
using System.IO;

using A2v10.Infrastructure;
using A2v10.Xaml.Report;

namespace A2v10.ReportEngine.Pdf;

internal class RenderContext
{
	private readonly IReportLocalizer _localizer;

	private readonly CultureInfo _formatProvider;
	private readonly String _rootPath;
	private readonly String _templatePath;

	public RenderContext(String rootPath, String templatePath, IReportLocalizer localizer, ExpandoObject model, String? code)
	{
		_localizer = localizer;
		_rootPath = rootPath;
		_templatePath = templatePath;
		DataModel = model;
		var  clone = _localizer.CurrentCulture.Clone();
		if (clone is CultureInfo cloneCI && cloneCI != null)
			_formatProvider = cloneCI;
		else
			throw new ArgumentNullException(nameof(_localizer), "Invalid Current culture");
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

		if (!String.IsNullOrEmpty(format))
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
		var pathDir = Path.GetDirectoryName(fullPath) ?? String.Empty;
		if (!pathDir.StartsWith(_rootPath, StringComparison.InvariantCultureIgnoreCase))
			throw new InvalidDataException("Invalid path. You can place files in the application folder only.");
		return File.ReadAllBytes(fullPath);
	}

	public Byte[]? GetValueAsByteArray(Object value, String propertyName)
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
		var objVal = Engine.EvaluateValue(bindRuntime.Expression[..lastDot]);
		if (objVal == null || objVal is not ExpandoObject eoVal)
			return null;
		return eoVal.Get<Byte[]>(bindRuntime.Expression[(lastDot + 1)..]);
	}

	public String? GetValueAsString(Object value, String propertyName = "Content")
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
				var val = Engine.EvaluateValue(contBind.Expression);
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
				var val = Engine.EvaluateValue(contBind.Expression);
				if (val != null)
					return ValueToString(val, contBind.DataType, contBind.Format);
			}
		}
		return null;
	}

	public Boolean IsVisible(XamlElement elem)
	{
		var ifbind = elem.GetBindRuntime("If");
		if (ifbind == null)
		{
			if (elem.If != null && !elem.If.Value)
				return false;
			return true;
		}
		var val = Engine.EvaluateValue(ifbind.Expression);
		if (val is Boolean boolVal)
			return boolVal;
		else if (val == null)
			return false;
		return true;
	}

	public void ApplyTextStyle(TextStyle textStyle)
	{
	}
}

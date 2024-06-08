// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System.Globalization;
using System.Text.RegularExpressions;

namespace A2v10.Services.Interop;

public enum CellKind
{
	Normal,
	Null,
	Span
}

public struct CellSpan
{
	public Int32 Row;
	public Int32 Col;
}

public partial class ExCell
{
	public CellSpan Span { get; set; }
	public String Value { get; set; } = String.Empty;

	public CellKind Kind { get; set; }

	public DataType DataType { get; set; }
	public UInt32 StyleIndex { get; set; }

	const String SPACECOMMA = @"[\s,]";
	const String SPACEONLY = @"[\s]";
#if NET7_0_OR_GREATER
	[GeneratedRegex(SPACECOMMA, RegexOptions.None, "en-US")]
	private static partial Regex SpaceCommaRegex();
	
	[GeneratedRegex(SPACEONLY, RegexOptions.None, "en-US")]
	private static partial Regex SpaceOnlyRegex();
#else
	private static Regex SPACECOMMA_REGEX => new(SPACECOMMA, RegexOptions.Compiled);
	private static Regex SpaceCommaRegex() => SPACECOMMA_REGEX;
	private static Regex SPACEONLY_REGEX => new(SPACEONLY, RegexOptions.Compiled);
	private static Regex SpaceOnlyRegex() => SPACEONLY_REGEX;
#endif

	static String NormalizeNumber(String number, IFormatProvider format)
	{
		if (String.IsNullOrEmpty(number))
			return String.Empty;
		if (Decimal.TryParse(number, NumberStyles.Number, format, out Decimal result))
			return result.ToString(CultureInfo.InvariantCulture);
		if (number.Contains('.'))
			return SpaceCommaRegex().Replace(number, String.Empty);
		else
			return SpaceOnlyRegex().Replace(number, String.Empty).Replace(',', '.');
	}

	static String NormalizePercent(String number, IFormatProvider format)
	{
        if (number.EndsWith('%'))
			number = number[..^1];
        var val = NormalizeNumber(number, format);
		if (String.IsNullOrEmpty(val))
			return val;
		if (Decimal.TryParse(val, out Decimal result))
			return (result / 100).ToString(CultureInfo.InvariantCulture);
		return "#ERR";
	}


    static String NormalizeDate(String text, IFormatProvider format)
	{
		if (String.IsNullOrEmpty(text))
			return String.Empty;

		if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
			return dt.ToOADate().ToString(CultureInfo.InvariantCulture);

		if (DateTime.TryParse(text, format, DateTimeStyles.None, out dt))
			return dt.ToOADate().ToString(CultureInfo.InvariantCulture);

		throw new ExportToExcelException($"Invalid date {text}");
	}

	static String NormalizeDateTime(String text)
	{
		if (String.IsNullOrEmpty(text))
			return String.Empty;
		if (DateTime.TryParseExact(text, "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
			return dt.ToOADate().ToString(CultureInfo.InvariantCulture);
		throw new ExportToExcelException($"Invalid datetime {text}");
	}

	static String NormalizeTime(String text)
	{
		if (String.IsNullOrEmpty(text))
			return String.Empty;
		if (DateTime.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
		{
			DateTime oaBaseDate = new(1899, 12, 30);
			return oaBaseDate.Add(dt.TimeOfDay).ToOADate().ToString(CultureInfo.InvariantCulture);
		}
		throw new ExportToExcelException($"Invalid time {text}");
	}

	public Style GetStyle(ExRow row, String strClasses)
	{
		var cls = Utils.ParseClasses(strClasses);
		var align = row.Align;
		if (cls.Align != HorizontalAlign.NotSet)
			align = cls.Align;
		return new Style()
		{
			DataType = DataType,
			RowRole = row.Role,
			RowKind = row.Kind,
			Align = align,
			VAlign = cls.VAlign,
			Bold = cls.Bold,
			Indent = cls.Indent,
			Underline = cls.Underline
		};
	}

	public static ExCell Create(Object? val)
	{
		var cell = new ExCell();
		if (val is String strVal)
		{
			cell.Value = strVal;
			cell.DataType = DataType.StringPlain;
		}
		else if (val is Decimal decVal)
		{
			cell.Value = decVal.ToString(CultureInfo.InvariantCulture);
			cell.DataType = DataType.Currency;
		}
		else if (val is Double dblVal)
		{
			cell.Value = dblVal.ToString(CultureInfo.InvariantCulture);
			cell.DataType = DataType.Number;
		}
		else if (val is Int32 int32Val)
		{
			cell.Value = int32Val.ToString();
			cell.DataType = DataType.Number;
		}
		else if (val is Int64 int64Val)
		{
			cell.Value = int64Val.ToString();
			cell.DataType = DataType.Number;
		}
		else if (val is DateTime dateVal)
		{
			cell.Value = dateVal.ToOADate().ToString(CultureInfo.InvariantCulture);
			if (dateVal.Minute == 0 && dateVal.Second == 0)
				cell.DataType = DataType.Date;
			else
				cell.DataType = DataType.DateTime;
		}
		else if (val is Boolean boolVal)
		{
			cell.Value = boolVal ? "1" : "0";
			cell.DataType = DataType.Boolean;
		}
		else if (val is TimeSpan timeVal)
		{
			DateTime oaBaseDate = new(1899, 12, 30);
			cell.Value = oaBaseDate.Add(timeVal).ToOADate().ToString(CultureInfo.InvariantCulture);
			cell.DataType = DataType.Time;
		}
		return cell;
	}
	public void SetValue(String text, String? dataType, IFormatProvider numberFormat, IFormatProvider dateFormat)
	{
		if (text.Contains('\n'))
			dataType = "string";
		switch (dataType)
		{
			case null:
			case "":
			case "string":
				DataType = DataType.String;
				Value = text;
				break;
			case "currency":
				DataType = DataType.Currency;
				Value = NormalizeNumber(text, numberFormat);
				break;
			case "number":
				DataType = DataType.Number;
				Value = NormalizeNumber(text, numberFormat);
				break;
			case "date":
				DataType = DataType.Date;
				Value = NormalizeDate(text, dateFormat);
				break;
			case "datetime":
				DataType = DataType.DateTime;
				Value = NormalizeDateTime(text);
				break;
			case "time":
				DataType = DataType.Time;
				Value = NormalizeTime(text);
				break;
			case "percent":
				DataType = DataType.Percent;
				Value = NormalizePercent(text, numberFormat);
				break;
			default:
				Value = text;
				break;
		}
	}

	public static String Reference(Int32 row, Int32 col)
	{
		return $"{Index2Col(col)}{row + 1}";
	}

	static String Index2Col(Int32 index)
	{
		Int32 q = index / 26;

		if (q > 0)
			return Index2Col(q - 1) + (Char)((Int32)'A' + (index % 26));
		else
			return "" + (Char)((Int32)'A' + index);
	}

	public String? MergeReference(Int32 row, Int32 col)
	{
		if (Span.Col <= 1 && Span.Row <= 1)
			return null;
		var colDelta = Span.Col > 1 ? Span.Col - 1 : 0;
		var rowDelta = Span.Row > 1 ? Span.Row - 1 : 0;
		return $"{Index2Col(col)}{row + 1}:{Index2Col(col + colDelta)}{row + 1 + rowDelta}";
	}
}

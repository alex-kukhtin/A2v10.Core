﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.RegularExpressions;

namespace A2v10.Services.Interop.ExportTo;

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

public class ExCell
{
    public CellSpan Span { get; set; }
    public String Value { get; set; } = String.Empty;

    public CellKind Kind { get; set; }

    public DataType DataType { get; set; }
    public UInt32 StyleIndex { get; set; }

    static String NormalizeNumber(String number, IFormatProvider format)
    {
        if (Decimal.TryParse(number, NumberStyles.Number, format, out Decimal result))
            return result.ToString(CultureInfo.InvariantCulture);
        if (number.IndexOf(".") != -1)
            return new Regex(@"[\s,]").Replace(number, String.Empty);
        else
            return new Regex(@"[\s]").Replace(number, String.Empty).Replace(",", ".");
    }

    static String NormalizeDate(String text)
    {
        if (String.IsNullOrEmpty(text))
            return String.Empty;
        if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
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

    public void SetValue(String text, String? dataType, IFormatProvider format)
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
                Value = NormalizeNumber(text, format);
                break;
            case "number":
                DataType = DataType.Number;
                Value = NormalizeNumber(text, format);
                break;
            case "date":
                DataType = DataType.Date;
                Value = NormalizeDate(text);
                break;
            case "datetime":
                DataType = DataType.DateTime;
                Value = NormalizeDateTime(text);
                break;
            case "time":
                DataType = DataType.Time;
                Value = NormalizeTime(text);
                break;
            default:
                Value = text;
                break;
        }
    }

    public String Reference(Int32 row, Int32 col)
    {
        return $"{Index2Col(col)}{row + 1}";
    }

    String Index2Col(Int32 index)
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
        //var rs = Span.Row - 1;
        return $"{Index2Col(col)}{row + 1}:{Index2Col(col + colDelta)}{row + 1 + rowDelta}";
    }
}

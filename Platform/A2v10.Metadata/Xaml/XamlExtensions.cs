// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal static class XamlExtensions
{

    public static String? Localize(this String? source)
    {
        if (source == null) 
            return null;
        if (source.StartsWith('@'))
            return $"@[{source[1..]}]";
        return source.Replace("\"", "&quot;");
    }


    internal static String LocalizeLabel(this ReportItemMetadata item)
    {
        return item.Label.Localize() ?? $"@[{item.Column}]";
    }

    internal static DataType ToXamlDataType(this FormColumnType column) =>
        column switch
        {
            FormColumnType.Date => DataType.Date,
            FormColumnType.DateTime => DataType.DateTime,
            FormColumnType.Currency => DataType.Currency,
            FormColumnType.Number => DataType.Number,
            _ => DataType.String,
        };

    internal static ColumnRole ToXamlColumnRole(this FormColumnType column) =>
        column switch
        {
            FormColumnType.Id => ColumnRole.Id,
            FormColumnType.Date or FormColumnType.DateTime => ColumnRole.Date,
            FormColumnType.Currency or FormColumnType.Number => ColumnRole.Number,
            _ => ColumnRole.Default,
        };

    internal static FormColumnType ToFormDataType(this ColumnType column) =>
        column switch
        {
            ColumnType.Id => FormColumnType.Id,
            ColumnType.Ref => FormColumnType.Ref,
            ColumnType.Date => FormColumnType.Date,
            ColumnType.DateTime => FormColumnType.DateTime,
            ColumnType.Money => FormColumnType.Currency,
            ColumnType.Float => FormColumnType.Number,
            _ => FormColumnType.String,
        };

    internal static SheetCell BindSheetCell(this ReportItemMetadata item, String? prefix = null)
    {
        var bind = item.DataType switch
        {
            ColumnType.Money => new BindSum($"{prefix}{item.Column}"),
            ColumnType.Float => new BindNumber($"{prefix}{item.Column}"),
            _ => new Bind($"{prefix}{item.Column}")
        };
        var align = item.DataType switch
        {
            ColumnType.Money => TextAlign.Right,
            ColumnType.Float => TextAlign.Right,
            _ => TextAlign.Left
        };
        return new SheetCell()
        {
            Align = align,
            Bindings = b => b.SetBinding(nameof(SheetCell.Content), bind)
        };
    }
}

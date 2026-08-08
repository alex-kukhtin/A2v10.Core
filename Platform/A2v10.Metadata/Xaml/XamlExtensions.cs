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

    internal static DataType ToXamlDataType(this ColumnType column) =>
        column switch
        {
            ColumnType.Date => DataType.Date,
            ColumnType.DateTime => DataType.DateTime,
            ColumnType.Money or ColumnType.Amount or ColumnType.Price => DataType.Currency,
            ColumnType.Float or ColumnType.Decimal or ColumnType.Qty => DataType.Number,
            _ => DataType.String,
        };

    internal static String ToXamlSemanticClass(this ColumnType column) =>
        $"dom-{column.ToString().ToLowerInvariant()}";

    internal static TextAlign ToXamlAlign(this ColumnType column) =>
        column switch
        {
            ColumnType.Date or ColumnType.DateTime => TextAlign.Center,
            ColumnType.Float or ColumnType.Decimal or ColumnType.Money or
                ColumnType.Price or ColumnType.Amount or ColumnType.Qty => TextAlign.Right,
            ColumnType.RowNumber => TextAlign.Right,
            ColumnType.Bit => TextAlign.Center,
            _ => TextAlign.Default,
        };


    internal static ColumnRole ToXamlColumnRole(this ColumnType column) =>
        column switch
        {
            ColumnType.Id => ColumnRole.Id,
            ColumnType.Date or ColumnType.DateTime => ColumnRole.Date,
            ColumnType.Money or ColumnType.Decimal or ColumnType.Float or
                ColumnType.Amount or ColumnType.Price => ColumnRole.Number,
            _ => ColumnRole.Default,
        };

    internal static SheetCell BindSheetCell(this ReportItemMetadata item, String? prefix = null)
    {
        var bind = item.DataType switch
        {
            ColumnType.Money or ColumnType.Amount => new BindSum($"{prefix}{item.Column}"),
            ColumnType.Float or ColumnType.Qty => new BindNumber($"{prefix}{item.Column}"),
            _ => new Bind($"{prefix}{item.Column}")
        };
        var align = item.DataType switch
        {
            ColumnType.Money or ColumnType.Amount or ColumnType.Qty or ColumnType.Float 
                or ColumnType.Price or ColumnType.Factor => TextAlign.Right,
            ColumnType.Date => TextAlign.Right,
            _ => TextAlign.Left
        };
        return new SheetCell()
        {
            Align = align,
            Bindings = b => b.SetBinding(nameof(SheetCell.Content), bind)
        };
    }
}

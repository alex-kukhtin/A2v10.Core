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

    /* A number: aligned right, a number column, ordered before the rest of a row. Asked as one word
     * so a new domain cannot land in some of these sites and miss the others.
     */
    internal static Boolean IsNumber(this ColumnType column) =>
        column.IsSum() || column is ColumnType.Price or ColumnType.Percent or ColumnType.Factor
            or ColumnType.Integer or ColumnType.Number or ColumnType.Decimal or ColumnType.Float;

    // the fork inside numbers: a sum shows at least two decimals, any other number as many as it has
    internal static Boolean IsSum(this ColumnType column) =>
        column is ColumnType.Amount or ColumnType.Qty or ColumnType.Money;

    internal static DataType ToXamlDataType(this ColumnType column) =>
        column switch
        {
            ColumnType.Date => DataType.Date,
            ColumnType.DateTime => DataType.DateTime,
            // stored as a fraction: the client multiplies by 100 and adds '%'
            ColumnType.Percent => DataType.Percent,
            _ when column.IsSum() => DataType.Currency,
            _ when column.IsNumber() => DataType.Number,
            _ => DataType.String,
        };

    internal static String ToXamlSemanticClass(this ColumnType column) =>
        $"dom-{column.ToString().ToLowerInvariant()}";

    internal static TextAlign ToXamlAlign(this ColumnType column) =>
        column switch
        {
            ColumnType.Date or ColumnType.DateTime => TextAlign.Center,
            ColumnType.RowNumber => TextAlign.Right,
            _ when column.IsNumber() => TextAlign.Right,
            ColumnType.Bit or ColumnType.Boolean => TextAlign.Center,
            _ => TextAlign.Default,
        };


    internal static ColumnRole ToXamlColumnRole(this ColumnType column) =>
        column switch
        {
            ColumnType.Id => ColumnRole.Id,
            // a number is as wide as its pattern and never wider: the grid gives it exactly that
            ColumnType.Autonum => ColumnRole.Fit,
            ColumnType.Date or ColumnType.DateTime => ColumnRole.Date,
            ColumnType.Bit or ColumnType.Boolean => ColumnRole.CheckBox,
            _ when column.IsNumber() => ColumnRole.Number,
            _ => ColumnRole.Default,
        };

    internal static SheetCell BindSheetCell(this ReportItemMetadata item, String? prefix = null)
    {
        var type = item.DataType;
        Bind bind = type switch
        {
            _ when type.IsSum() => new BindSum($"{prefix}{item.Column}"),
            _ when type.IsNumber() => new BindNumber($"{prefix}{item.Column}") { DataType = type.ToXamlDataType() },
            _ => new Bind($"{prefix}{item.Column}")
        };
        var align = type.IsNumber() || type == ColumnType.Date ? TextAlign.Right : TextAlign.Left;
        return new SheetCell()
        {
            Align = align,
            Bindings = b => b.SetBinding(nameof(SheetCell.Content), bind)
        };
    }
}

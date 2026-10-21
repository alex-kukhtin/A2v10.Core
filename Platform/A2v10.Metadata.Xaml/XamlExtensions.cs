// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using A2v10.Xaml;

namespace A2v10.Metadata;

// a domain as the view engine draws it; what a domain IS (IsNumber, IsCurrency) is the layer's, ColumnTypePredicates
internal static class XamlExtensions
{
    internal static DataType ToXamlDataType(this ColumnType column) =>
        column switch
        {
            ColumnType.Date => DataType.Date,
            ColumnType.DateTime => DataType.DateTime,
            // a percent is stored as written (20 is 20%), so it draws as a number; DataType.Percent scales by 100
            _ when column.IsCurrency() => DataType.Currency,
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
            ColumnType.Boolean => TextAlign.Center,
            _ => TextAlign.Default,
        };


    internal static ColumnRole ToXamlColumnRole(this ColumnType column) =>
        column switch
        {
            ColumnType.Id => ColumnRole.Id,
            // a number is as wide as its pattern and never wider: the grid gives it exactly that
            ColumnType.Autonum => ColumnRole.Fit,
            ColumnType.Date or ColumnType.DateTime => ColumnRole.Date,
            ColumnType.Boolean => ColumnRole.CheckBox,
            _ when column.IsNumber() => ColumnRole.Number,
            _ => ColumnRole.Default,
        };

    internal static SheetCell BindSheetCell(this ReportItemMetadata item, String? prefix = null)
    {
        var type = item.DataType;
        Bind bind = type switch
        {
            _ when type.IsCurrency() => new BindSum($"{prefix}{item.Column}"),
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

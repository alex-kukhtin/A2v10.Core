// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class FormExtensions
{
    public static FormItemIs Column2Is(this TableColumn column)
    {
        if (column.IsReference)
            return FormItemIs.Selector;
        return column.DataType switch
        {
            ColumnDataType.DateTime or ColumnDataType.Date => FormItemIs.DatePicker,
            ColumnDataType.Bit => FormItemIs.CheckBox,
            _ => FormItemIs.TextBox,
        };
    }
    public static ItemDataType ToItemDataType(this TableColumn column)
    {
        return column.DataType switch
        {
            ColumnDataType.NChar or ColumnDataType.NVarChar => ItemDataType.String,
            ColumnDataType.Bit => ItemDataType.Boolean,
            ColumnDataType.Date => ItemDataType.Date,
            ColumnDataType.DateTime => ItemDataType.DateTime,
            ColumnDataType.Money => ItemDataType.Currency,
            _ => ItemDataType.Unknown,
        };
    }

    public static String? ToWidth(this ColumnDataType dt)
    {
        return dt switch
        {
            ColumnDataType.Money => "12rem",
            ColumnDataType.Date or ColumnDataType.DateTime => "12rem",
            _ => null
        };
    }
}

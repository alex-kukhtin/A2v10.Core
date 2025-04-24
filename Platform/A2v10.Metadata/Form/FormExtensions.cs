// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

internal static class FormExtensions
{
    public static FormItemIs Column2Is(this TableColumn column)
    {
        if (column.IsReference)
            return FormItemIs.Selector;
        if (column.Role.HasFlag(TableColumnRole.RowNo))
            return FormItemIs.Content;
        return column.DataType switch
        {
            ColumnDataType.DateTime or ColumnDataType.Date => FormItemIs.DatePicker,
            ColumnDataType.Bit => FormItemIs.CheckBox,
            _ => FormItemIs.TextBox,
        };
    }

    public static String? ToColumnWidth(this TableColumn column)
    {
        if (column.Role.HasFlag(TableColumnRole.RowNo))
            return "1px";
        if (column.DataType == ColumnDataType.Money || column.DataType == ColumnDataType.Float)
            return "10rem";
        return null;
    }

    public static ItemDataType ToItemDataType(this TableColumn column)
    {
        return column.DataType switch
        {
            ColumnDataType.Id => ItemDataType.Id,
            ColumnDataType.NChar or ColumnDataType.NVarChar => ItemDataType.String,
            ColumnDataType.Bit => ItemDataType.Boolean,
            ColumnDataType.Date => ItemDataType.Date,
            ColumnDataType.DateTime => ItemDataType.DateTime,
            ColumnDataType.Money => ItemDataType.Currency,
            ColumnDataType.Float => ItemDataType.Number,
            ColumnDataType.Int => ItemDataType.Number,
            _ => ItemDataType._,
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

    public static IEnumerable<TableColumn> VisibleColumns(this TableMetadata table, AppMetadata appMeta)
    {
        Boolean IsVisible(TableColumn column)
        {
            if (appMeta.IdDataType == ColumnDataType.Uniqueidentifier && column.Role.HasFlag(TableColumnRole.PrimaryKey))
                return false;
            if (column.DataType == ColumnDataType.Stream)
                return false;
            return !column.Role.HasFlag(TableColumnRole.Void)
                && !column.Role.HasFlag(TableColumnRole.IsFolder)
                && !column.Role.HasFlag(TableColumnRole.Kind)
                && !column.Role.HasFlag(TableColumnRole.IsSystem)
                && !column.Role.HasFlag(TableColumnRole.SystemName)
                && !column.Role.HasFlag(TableColumnRole.Parent);
        }

        return table.Columns.Where(IsVisible).OrderBy(c => c.Order);
    }

    public static IEnumerable<TableColumn> EditableColumns(this TableMetadata table)
    {
        Boolean IsVisible(TableColumn column)
        {
            return column.Role != TableColumnRole.Void
                && !column.Role.HasFlag(TableColumnRole.PrimaryKey)
                && !column.Role.HasFlag(TableColumnRole.IsFolder)
                && !column.Role.HasFlag(TableColumnRole.IsSystem)
                && !column.Role.HasFlag(TableColumnRole.SystemName)
                && !column.Role.HasFlag(TableColumnRole.Kind);
        }

        return table.Columns.Where(IsVisible).OrderBy(c => c.Order);
    }
}

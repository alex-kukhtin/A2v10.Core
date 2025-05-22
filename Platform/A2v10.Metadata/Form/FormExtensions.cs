// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

internal static class FormExtensions
{
    public static FormItemIs Column2Is(this TableColumn column)
    {
        if (column.IsReference)
            return column.DataType == ColumnDataType.Enum 
                ? FormItemIs.ComboBox : FormItemIs.Selector;
        if (column.Role.HasFlag(TableColumnRole.RowNo))
            return FormItemIs.Content;
        if (!String.IsNullOrEmpty(column.Computed))
            return FormItemIs.Static;

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

    public static FormItemProps? IndexColumnProps(this TableColumn column)
    {
        var noWrap = column.Role.HasFlag(TableColumnRole.Code) || column.Role.HasFlag(TableColumnRole.Number); 
        var lineClamp = column.IsString && (column.MaxLength == 0 || column.MaxLength > Constants.MultilineThreshold) ? 2 : 0;

        if (noWrap || lineClamp > 0)
        {
            return new FormItemProps()
            {
                LineClamp = lineClamp,
                Fit = noWrap,
                NoWrap = noWrap,
            };
        }
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
            var sysColumns = 
                  TableColumnRole.Void
                | TableColumnRole.IsFolder
                | TableColumnRole.IsSystem
                | TableColumnRole.SystemName
                | TableColumnRole.Kind
                | TableColumnRole.Parent;

            return (column.Role & sysColumns) == 0;
        }

        return table.Columns.Where(IsVisible).OrderBy(c => c.Order);
    }

    public static IEnumerable<TableColumn> EditableColumns(this TableMetadata table)
    {
        var hiddenColumns =
              TableColumnRole.Void
            | TableColumnRole.PrimaryKey
            | TableColumnRole.IsFolder
            | TableColumnRole.IsSystem
            | TableColumnRole.SystemName
            | TableColumnRole.Kind
            | TableColumnRole.Parent;

        return table.Columns.Where(c => (c.Role & hiddenColumns) == 0).OrderBy(c => c.Order);
    }
}

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
            return FormItemIs.Selector;
        else if (column.IsEnum)
            return FormItemIs.ComboBox;
        if (column.Role.HasFlag(TableColumnRole.RowNo))
            return FormItemIs.Content;
        if (!String.IsNullOrEmpty(column.Computed))
            return FormItemIs.Static;

        return column.Type switch
        {
            ColumnType.DateTime or ColumnType.Date => FormItemIs.DatePicker,
            ColumnType.Bit => FormItemIs.CheckBox,
            _ => FormItemIs.TextBox,
        };
    }

    public static String? ToColumnWidth(this TableColumn column)
    {
        if (column.Role.HasFlag(TableColumnRole.RowNo))
            return "1px";
        if (column.Type == ColumnType.Money || column.Type == ColumnType.Float || 
            column.Type == ColumnType.Decimal || column.Type == ColumnType.Int)
            return "10rem";
        return null;
    }

    public static FormItemProps? IndexColumnProps(this TableColumn column)
    {
        var noWrap = column.Role.HasFlag(TableColumnRole.Code) || column.Role.HasFlag(TableColumnRole.Number) || column.IsEnum; 
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
        return column.Type switch
        {
            ColumnType.Id => ItemDataType.Id,
            ColumnType.NChar or ColumnType.NVarChar => ItemDataType.String,
            ColumnType.Bit => ItemDataType.Boolean,
            ColumnType.Date => ItemDataType.Date,
            ColumnType.DateTime => ItemDataType.DateTime,
            ColumnType.Money => ItemDataType.Currency,
            ColumnType.Float or ColumnType.Int or ColumnType.Decimal 
                => ItemDataType.Number,
            _ => ItemDataType._,
        };
    }

    public static String? ToWidth(this ColumnType dt)
    {
        return dt switch
        {
            ColumnType.Money => "12rem",
            ColumnType.Float => "12rem",
            ColumnType.Decimal => "12rem",
            ColumnType.Date or ColumnType.DateTime => "12rem",
            _ => null
        };
    }

    public static IEnumerable<TableColumn> VisibleColumns(this TableMetadata table, AppMetadata appMeta)
    {
        Boolean IsVisible(TableColumn column)
        {
            if (appMeta.IdDataType == ColumnType.Uniqueidentifier && column.Role.HasFlag(TableColumnRole.PrimaryKey))
                return false;
            if (column.Type == ColumnType.Stream || column.Type == ColumnType.RowVersion)
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
        static Boolean isVisible(TableColumn column)
        {
            var hiddenColumns =
             TableColumnRole.Void
           | TableColumnRole.PrimaryKey
           | TableColumnRole.IsFolder
           | TableColumnRole.IsSystem
           | TableColumnRole.SystemName
           | TableColumnRole.Kind;

            return column.Type != ColumnType.Stream 
                && column.Type != ColumnType.RowVersion 
                && (column.Role & hiddenColumns) == 0;
        }

        return table.Columns.Where(c => isVisible(c)).OrderBy(c => c.IsMemo ? Int32.MaxValue : c.Order);
    }
}

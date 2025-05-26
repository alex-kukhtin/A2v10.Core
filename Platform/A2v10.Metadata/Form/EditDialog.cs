﻿// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    FormItem CreateControl(TableColumn column, Int32 row, Int32 col)
    {
        String? prm = null;
        if (column.IsReference)
            prm = column.Reference.EndpointPath();
        String? itemsSource = null;
        if (column.IsReference)
            itemsSource = column.Reference.RefTable;
        var colName = column.IsParent ? "Folder" : column.Name;
        return new FormItem()
        {
            Is = column.Column2Is(),
            Grid = (row != 0 || col != 0) ? new FormItemGrid(row, col) : null,
            Label = column.Label ?? $"@{colName}",
            Data = $"{_table.RealItemName}.{colName}",
            DataType = column.ToItemDataType(),
            Width = column.DataType.ToWidth(),
            Props = new FormItemProps()
            {
                Url = prm,
                ItemsSource = itemsSource,
                Multiline = column.DataType == ColumnDataType.String && 
                    (column.MaxLength > Constants.MultilineThreshold || column.MaxLength == 0),
                TabIndex = column.IsName ? 1 : 0,
                Required = column.Required,
                Folder = column.IsParent && _table.UseFolders,
            }
        };
    }

    private FormItem Content()
    {
        var hasDetails = _table.Details.Any();
        var memoColumn = _table.Columns.FirstOrDefault(c => c.IsMemo);

        var editableColumns = _table.EditableColumns();

        if (hasDetails && memoColumn != null)
            editableColumns = editableColumns
                .Where(c => !c.IsMemo).ToList();

        FormItem ContentGrid()
        {
            return new FormItem(FormItemIs.Grid)
            {
                Is = FormItemIs.Grid,
                Props = new FormItemProps()
                {
                    // rows + 1
                    Rows = String.Join(' ', Enumerable.Range(1, editableColumns.Count()).Select(c => "auto")),
                    Columns = "1fr"
                },
                Items = [.. editableColumns.Select((c, index) => CreateControl(c, index + 1, 1))]
            };
        }

        if (hasDetails)
        {
            IEnumerable<FormItem> EnumMemo()
            {
                if (memoColumn != null)
                    yield return CreateControl(memoColumn, 0, 0);
            }

            return new FormItem(FormItemIs.StackPanel)
            {
                CssClass = "dialog-details",
                Items = [
                    ContentGrid(),
                    new FormItem(FormItemIs.Tabs)
                    {
                        Data = $"{_table.RealItemName}.$$Tab",
                        CssClass = "details-tabbar",
                        Height = "20rem",
                        Items = [.. _table.Details.Select(d => DetailsTab(d, $"{_table.RealItemName}.{d.RealItemsName}", d.RealItemsName, d.RealItemsLabel, "catalog"))],
                    },
                    ..EnumMemo()
                ]
            };
        }
        return ContentGrid();
    }

    private Form CreateEditDialog()
    {
        return new Form()
        {
            Schema = _table.Schema,
            Table = _table.Name,
            Is = FormItemIs.Dialog,
            Label = _table.RealItemLabel,
            CssClass = "edit-item-dialog",
            EditWith = _table.EditWith,
            Data = _table.RealItemName,
            Items = [Content()],
            Buttons = [
                new FormItem(FormItemIs.Button)
                {
                    Label = "@SaveAndClose",
                    Command = new FormItemCommand(FormCommand.SaveAndClose),
                    Props = new FormItemProps() {
                        Style = ItemStyle.Primary,
                    }
                },
                new FormItem(FormItemIs.Button)
                {
                    Label = "@Cancel",
                    Command = new FormItemCommand(FormCommand.Close)
                }
            ]
        };
    }
}

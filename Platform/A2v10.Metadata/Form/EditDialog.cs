// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

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
        return new FormItem()
        {
            Is = column.Column2Is(),
            Grid = new FormItemGrid(row, col),
            Label = column.Label ?? $"@{column.Name}",
            Data = $"{_table.RealItemName}.{column.Name}",
            DataType = column.ToItemDataType(),
            Width = column.DataType.ToWidth(),
            Props = new FormItemProps()
            {
                Url = prm,
                ItemsSource = itemsSource,
                Multiline = column.DataType == ColumnDataType.String && 
                    (column.MaxLength > Constants.MultilineThreshold || column.MaxLength == 0),
                TabIndex = column.Role.HasFlag(TableColumnRole.Name) ? 1 : 0,
                Required = column.Required,
            }
        };
    }

    private FormItem Content()
    {
        var hasDetails = _table.Details.Any();
        var memoColumn = _table.Columns.FirstOrDefault(c => c.Name == "Memo");

        var editableColumns = _table.EditableColumns();
        if (hasDetails && memoColumn != null)
            editableColumns = editableColumns
                .Where(c => c.Name != "Memo").ToList();

        IEnumerable<FormItem> Controls()
        {
            Int32 row = 1;
            return editableColumns.Select(c => CreateControl(c, row++, 1));
        }


        FormItem ContentGrid()
        {
            return new FormItem(FormItemIs.Grid)
            {
                Is = FormItemIs.Grid,
                Props = new FormItemProps()
                {
                    // rows + 1
                    Rows = String.Join(' ', editableColumns.Select(c => "auto")
                        .Union(["auto"])),
                    Columns = "1fr"
                },
                Items = [.. Controls()]
            };
        }

        if (hasDetails)
        {
            IEnumerable<FormItem> EnumMemo()
            {
                if (memoColumn != null)
                    yield return CreateControl(memoColumn, 4, 1);
            }

            return new FormItem(FormItemIs.Grid)
            {
                Props = new FormItemProps()
                {
                    Rows = memoColumn != null ? "auto auto 1fr auto" : "auto auto 1fr",
                    Columns = "1fr"
                },
                Items = [
                    ContentGrid(),
                    new FormItem(FormItemIs.Tabs)
                    {
                        Data = $"{_table.RealItemName}.$$Tab",
                        Grid = new FormItemGrid(3, 1),
                        CssClass = "details-tabbar",
                        Items = [.. _table.Details.Select(d => DetailsTab(d, $"{_table.RealItemName}.{d.RealItemsName}", d.RealItemsName, d.RealItemsLabel, "details"))]
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

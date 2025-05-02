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
        return new FormItem()
        {
            Is = column.Column2Is(),
            Grid = new FormItemGrid(row, col),
            Label = column.Label ?? $"@{column.Name}",
            Data = $"{_table.RealItemName}.{column.Name}",
            DataType = column.ToItemDataType(),
            Props = new FormItemProps()
            {
                Url = prm,
                Multiline = column.DataType == ColumnDataType.String && 
                    (column.MaxLength > Constants.MultilineThreshold || column.MaxLength == 0),
            },
            Width = column.DataType.ToWidth()
        };
    }

    private FormItem Content()
    {
        IEnumerable<FormItem> Controls()
        {
            Int32 row = 1;
            return _table.EditableColumns().Select(c => CreateControl(c, row++, 1));
        }

        FormItem ContentGrid()
        {
            return new FormItem(FormItemIs.Grid)
            {
                Is = FormItemIs.Grid,
                Props = new FormItemProps()
                {
                    // rows + 1
                    Rows = String.Join(' ', _table.EditableColumns().Select(c => "auto")
                        .Union(["auto"])),
                    Columns = "1fr"
                },
                Items = [.. Controls()]
            };
        }

        var hasDetails = _table.Details.Any();
        var hasMemo = _table.Columns.Any(c => c.Name == "Memo");
        if (hasDetails)
        {
            return new FormItem(FormItemIs.Grid)
            {
                Props = new FormItemProps()
                {
                    Rows = hasMemo ? "auto auto 1fr auto" : "auto auto 1fr",
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
                    }
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

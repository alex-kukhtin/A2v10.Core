// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    public Form CreateBrowseDialog()
    {
        IEnumerable<FormItem> ToolbarButtons()
        {
            yield return new FormItem(FormItemIs.Button)
            {
                Label = "@Create",
                Command = new FormItemCommand(FormCommand.Create)
                {
                    Url = _table.EndpointPathUseBase(_baseTable),
                    Argument = "Parent.ItemsSource"
                }
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.EditSelected)
                {
                    Url = _table.EditEndpoint(_baseTable),
                    Argument = "Parent.ItemsSource"
                }
            };
            yield return new FormItem(FormItemIs.Separator);
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.Reload),
            };
            yield return new FormItem(FormItemIs.Aligner);
            yield return new FormItem(FormItemIs.SearchBox)
            {
                Data = "Parent.Filter.Fragment",
                Width = "20rem"
            };
        }

        return new Form()
        {
            Is = FormItemIs.Dialog,
            Data = _table.RealItemsName,
            UseCollectionView = true,
            Schema = _table.Schema,
            Table = _table.Name,
            EditWith = _table.EditWith,
            Label = $"@{_table.RealItemsName}.Browse",
            Width = "65rem",
            Items = [
                new FormItem()
                {
                    Is = FormItemIs.Grid,
                    Props = new FormItemProps() {
                        Rows = "auto 1fr auto",
                        Columns = "1fr",
                    },
                    Items = [
                        new FormItem() {
                            Is = FormItemIs.Toolbar,
                            Grid = new FormItemGrid(1, 1),
                            Items =  [..ToolbarButtons()]
                        },
                        new FormItem() {
                            Is = FormItemIs.DataGrid,
                            Grid = new FormItemGrid(2, 1),
                            Height = "30rem",
                            Command = new FormItemCommand(FormCommand.Select, _table.RealItemsName),
                            Data = "Parent.ItemsSource",
                            Items = [..IndexColumns()]
                        },
                        new FormItem() {
                            Is = FormItemIs.Pager,
                            Grid = new FormItemGrid(3, 1), 
                            Data = "Parent.Pager"
                        }
                    ]                        
                }
            ],
            Buttons = [
                new FormItem(FormItemIs.Button)
                {
                    Label = "@Select",
                    Command = new FormItemCommand(FormCommand.Select, _table.RealItemsName),
                    Props = new FormItemProps() 
                    {
                        Style = ItemStyle.Primary
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

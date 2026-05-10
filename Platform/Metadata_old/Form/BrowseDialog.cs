// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
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
                Width = "20rem",
                Props = new FormItemProps()
                {
                    TabIndex = 1
                }
            };
        }

        FormItem? CreateTaskpad()
        {
            if (!_table.UseFolders)
                return null;

            FormItem CreateFoldersView()
            {
                return new FormItem(FormItemIs.Panel)
                {
                    Label = "@Grouping",
                    Items = [
                        TreeToolbar(),
                        new FormItem(FormItemIs.TreeView)
                        {
                            Data = "Folders",
                            Height = "48vh",
                        }
                    ]
                };
            }

            IEnumerable<FormItem> Filters()
            {
                yield break;
            }

            IEnumerable<FormItem> TaskpadPanels()
            {
                if (_table.UseFolders)
                    yield return CreateFoldersView();
                var filters = Filters().ToList();
                if (filters.Count > 0)
                    yield return new FormItem(FormItemIs.Panel)
                    {
                        Label = "@Filters",
                        Items = [
                            new FormItem(FormItemIs.Grid)
                                {
                                    Props = new FormItemProps() {
                                        Rows = String.Join(' ', Enumerable.Range(1, filters.Count).Select(c => "auto")),
                                        Columns = "1fr",
                                    },
                                    Items = [..Filters()]
                                }
                        ]
                    };
            }

            return new FormItem(FormItemIs.Taskpad)
            {
                Items = [.. TaskpadPanels()],
            };
        }

        return new Form()
        {
            Is = FormItemIs.Dialog,
            Data = _table.UseFolders ? $"Folders.Selected({_table.RealItemsName})" : _table.RealItemsName,
            UseCollectionView = true,
            Schema = _table.Schema,
            Table = _table.Name,
            EditWith = _table.EditWith,
            Label = $"@{_table.RealItemsName}.Browse",
            Width = _table.UseFolders ? "85rem" : "65rem",
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
                            Command = new FormItemCommand(FormCommand.Select, "Parent.ItemsSource"),
                            Data = "Parent.ItemsSource",
                            Items = [..IndexColumns(false)]
                        },
                        new FormItem() {
                            Is = FormItemIs.Pager,
                            Grid = new FormItemGrid(3, 1), 
                            Data = "Parent.Pager"
                        }
                    ]                        
                }
            ],
            Taskpad = CreateTaskpad(),
            Buttons = [
                new FormItem(FormItemIs.Button)
                {
                    Label = "@Select",
                    Command = new FormItemCommand(FormCommand.Select, "Parent.ItemsSource"),
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

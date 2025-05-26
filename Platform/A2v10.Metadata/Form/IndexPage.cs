// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    IEnumerable<FormItem> IndexColumns()
    {
        if (_table.UseFolders)
            yield return new FormItem()
            {
                Is = FormItemIs.DataGridColumn,
                DataType = ItemDataType.Boolean,
                Data = "$checked",
            };
        foreach (var c in _table.VisibleColumns(_appMeta))
            yield return new FormItem()
            {
                Is = FormItemIs.DataGridColumn,
                DataType = c.ToItemDataType(),
                Data = c.IsReference ?
                    $"{c.Name}.{_refFields.First(r => r.Column.Name == c.Name).Table.NameField}"
                    : c.Name,
                Label = c.Label ?? $"@{c.Name}",
                Props = c.IndexColumnProps()
            };
    }

    private Form CreateIndexPage()
    {
        IEnumerable<FormItem> ToolbarButtons()
        {
            yield return FormBuild.Button(new FormItemCommand(FormCommand.Create)
                {
                    Url = _table.EndpointPathUseBase(_baseTable),
                    Argument = "Parent.ItemsSource"
                },
                "@Create"
            );
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.EditSelected)
                {
                    Url = _table.EditEndpoint(_baseTable),
                    Argument = "Parent.ItemsSource"
                }
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.DeleteSelected)
                {
                    Url = _table.EditEndpoint(_baseTable),
                    Argument = "Parent.ItemsSource"
                }
            };
            yield return new FormItem(FormItemIs.Separator);
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.Reload)
                {
                    Argument = _table.UseFolders ? $"Folders.Selected({_table.RealItemsName})" : null
                }
            };
            yield return new FormItem(FormItemIs.Aligner);
            yield return new FormItem(FormItemIs.SearchBox)
            {
                Width = "20rem",
                Data = "Parent.Filter.Fragment"
            };
        }

        FormItem? CreateTaskPad()
        {
            if (!_table.Columns.Any(c => c.IsReference) && !_table.UseFolders)
                return null;

            FormItem TreeToolbar()
            {
                return new FormItem(FormItemIs.Toolbar)
                {
                    Items = [
                        new FormItem(FormItemIs.Button)
                        {
                            Label = "@Create",
                            Command = new FormItemCommand(FormCommand.Create)
                            {
                                Url = _table.EndpointPath(),
                                Argument = "Folders"
                            }
                        },
                        new FormItem(FormItemIs.Button)
                        {
                            Command = new FormItemCommand(FormCommand.EditSelected)
                            {
                                Url = _table.EndpointPathUseBase(_baseTable),
                                Argument = "Folders"
                            }
                        },
                        new FormItem(FormItemIs.Button)
                        {
                            Command = new FormItemCommand(FormCommand.DeleteSelected)
                            {
                                Argument = "Folders"
                            }
                        },
                        new FormItem(FormItemIs.Separator),
                        new FormItem(FormItemIs.Button)
                        {
                            Command = new FormItemCommand(FormCommand.Reload)
                        }
                    ]
                };
            }

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
                            Height = "50vh",
                        }
                    ]
                };
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

            FormItem CreateFilter(TableColumn column, Int32 gridRow)
            {
                String? url = null;
                String? itemsSource = null;
                Int32 lineClamp = 0;
                Boolean showClear = false;
                if (column.IsReference && !column.IsEnum)
                {
                    url = column.Reference.EndpointPath();
                    lineClamp = 2;
                    showClear = true;
                }
                if (column.IsEnum)
                    itemsSource = column.Reference.RefTable;
                return new FormItem()
                {
                    Is = column.Column2Is(),
                    Label = column.Label ?? $"@{column.Name}",
                    Data = $"Parent.Filter.{column.Name}",
                    Grid = new FormItemGrid(gridRow, 1),
                    Props = new FormItemProps()
                    {
                        Url = url,
                        Placeholder = $"@{column.Reference.RefTable}.All",
                        ShowClear = showClear,
                        LineClamp = lineClamp,
                        ItemsSource = itemsSource,
                        Highlight = true
                    }
                };
            }

            var tableRefCols = _table.Columns.Where(c => c.IsReference && !c.Role.HasFlag(TableColumnRole.Parent));
            if (_baseTable != null && _baseTable.Schema == "op")
                tableRefCols = tableRefCols.Where(c => c.DataType != ColumnDataType.Operation);
            
            tableRefCols = tableRefCols.OrderBy(c => c.Order);

            IEnumerable<FormItem> Filters()
            {
                Int32 gridRow = 1;  
                if (_table.HasPeriod())
                    yield return new FormItem(FormItemIs.PeriodPicker)
                    {
                        Data = "Parent.Filter.Period",
                        Label = "@Period",
                        Grid = new FormItemGrid(gridRow++, 1)
                    };
                foreach (var c in tableRefCols)
                    yield return CreateFilter(c, gridRow++);
            }

            return new FormItem(FormItemIs.Taskpad)
            {
                Items = [..TaskpadPanels()],
            };
        }

        IEnumerable<String> FilterNames()
        {
            if (_table.HasPeriod())
                yield return "Period";
            foreach (var column in _table.Columns.Where(c => c.IsReference))
                yield return column.Name;
        }

        return new Form()
        {
            Is = FormItemIs.Page,
            UseCollectionView = true,
            Schema = _table.Schema,
            Table = _table.Name,
            Data = _table.UseFolders ? $"Folders.Selected({_table.RealItemsName})" : _table.RealItemsName,
            EditWith = _table.EditWith,
            Label = _baseTable?.RealItemsLabel ?? _table.RealItemsLabel,
            Props = new FormItemProps()
            {
                Filters = String.Join(',', FilterNames()) 
            },
            Items = [
                new FormItem() {
                    Is = FormItemIs.Grid,
                    Props = new FormItemProps() {
                        Rows = "auto 1fr auto",
                        Columns = "1fr",
                    },
                    Height = "100%",
                    Items = [
                        new FormItem() {
                            Is = FormItemIs.Toolbar,
                            Grid = new FormItemGrid(1, 1),
                            Items = [..ToolbarButtons()]
                        },
                        new FormItem() {
                            Is = FormItemIs.DataGrid,
                            Data = "Parent.ItemsSource",
                            Height = "100%",
                            Grid = new FormItemGrid(2, 1),
                            Items = [..IndexColumns()],
                            Command = new FormItemCommand(FormCommand.EditSelected)
                            {
                                Url = _table.EditEndpoint(_baseTable),
                                Argument = "Parent.ItemsSource"
                            }
                        },
                        new FormItem() {
                            Is = FormItemIs.Pager,
                            Grid = new FormItemGrid(3, 1),
                            Data = "Parent.Pager"
                        }
                    ]
                }
            ],
            Taskpad = CreateTaskPad()
        };
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    private Form CreateIndexPage()
    {
        IEnumerable<FormItem> Columns() { 

            return _table.VisibleColumns(_appMeta).Select(
                c => new FormItem()
                {
                    Is = FormItemIs.DataGridColumn,
                    DataType = c.ToItemDataType(),
                    Data = c.IsReference ? $"{c.Name}.{_appMeta.NameField}"  : c.Name,
                    Label = $"@{c.Name}"
                }
            ); 
        }

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
                    Url = _table.EndpointPathUseBase(_baseTable),
                    Argument = "Parent.ItemsSource"
                }
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.Reload),
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
            if (!_table.Columns.Any(c => c.IsReference))
                return null;
            
            IEnumerable<FormItem> CreatePeriod()
            {
                if (_table.HasPeriod())
                    yield return new FormItem(FormItemIs.PeriodPicker)
                    {
                        Data = "Parent.Filter.Period",
                        Label = "@Period"
                    };
                yield break;
            }

            FormItem CreateFilter(TableColumn column)
            {
                return new FormItem(FormItemIs.Selector)
                {
                    Label = $"@{column.Name}",
                    Data = $"Parent.Filter.{column.Name}",
                    Props = new FormItemProps()
                    {
                        Url = column.Reference.EndpointPath(),
                        Placeholder = $"@{column.Reference.RefTable}.All",
                        ShowClear = true,
                    }
                };
            }

            var tableCols = _table.Columns.Where(c => c.IsReference);
            if (_baseTable != null && _baseTable.Schema == "op")
                tableCols = tableCols.Where(c => c.DataType != ColumnDataType.Operation);

            var columns =  CreatePeriod().Union(tableCols.Select(c => CreateFilter(c)));

            return new FormItem(FormItemIs.Taskpad)
            {
                Items = [
                    new FormItem(FormItemIs.Panel) {
                        Label = "@Filters",
                        Items = [
                            new FormItem(FormItemIs.Grid) 
                            {
                                Items = [..columns]
                            }
                        ]
                    }
                ]
            };
        }

        String periodFilter = _table.HasPeriod() ? "Period," : String.Empty;

        return new Form()
        {
            Is = FormItemIs.Page,
            UseCollectionView = true,
            Schema = _table.Schema,
            Table = _table.Name,
            Data = _table.RealItemsName,
            Label = $"@{_table.RealItemsName}",
            Props = new FormItemProps()
            {
                Filters = $"{periodFilter}{String.Join(',', _table.RefFields().Select(c => c.Column.Name))}"
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
                            Items = [..Columns()],
                            Command = new FormItemCommand(FormCommand.EditSelected)
                            {
                                Url = _table.EndpointPath(),
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

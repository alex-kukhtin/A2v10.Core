// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class FormBuilder
{
    private async Task<Form> CreateIndexFormAsync()
    {
        var tableMeta = await _metaProvider.GetSchemaAsync(_dataSource, _meta.Schema, _meta.Name);
        var appMeta = await _metaProvider.GetAppMetadataAsync(_dataSource);

        IEnumerable<FormItem> Columns() { 

            return VisibleColumns(tableMeta, appMeta).Select(
                c => new FormItem()
                {
                    Is = FormItemIs.DataGridColumn,
                    DataType = c.ToItemDataType(),
                    Data = c.IsReference ? $"{c.Name}.{appMeta.NameField}"  : c.Name,
                    Label = $"@{c.Name}"
                }
            ); 
        }

        IEnumerable<FormItem> ToolbarButtons()
        {
            yield return new FormItem(FormItemIs.Button)
            {
                Label = "@Create",
                Command = new FormItemCommand(FormCommand.Create,
                    _metaProvider.GetOrAddEndpointPath(_dataSource, _meta)),
            };
            yield return tableMeta.EditWith switch
            {
                EditWithMode.Page => new FormItem(FormItemIs.Button)
                {
                    Command = new FormItemCommand()
                    {
                        Command = FormCommand.Open,
                        Url = _metaProvider.GetOrAddEndpointPath(_dataSource, _meta),
                        Argument = "Parent.ItemsSource"
                    }
                },
                EditWithMode.Dialog => new FormItem(FormItemIs.Button)
                {
                    Command = new FormItemCommand()
                    {
                        Command = FormCommand.Edit,
                        Url = _metaProvider.GetOrAddEndpointPath(_dataSource, _meta),
                        Argument = "Parent.ItemsSource"
                    }
                },
                _ => throw new InvalidOperationException($"Implement Command {tableMeta.EditWith}")
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
            if (!tableMeta.Columns.Any(c => c.IsReference))
                return null;

            FormItem CreateFilter(TableColumn column)
            {
                return new FormItem(FormItemIs.Selector)
                {
                    Label = $"@{column.Name}",
                    Data = $"Parent.Filter.{column.DataType}"
                };
            }

            var columns = tableMeta.Columns.Where(c => c.IsReference).Select(c => CreateFilter(c));

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

        return new Form()
        {
            Is = FormItemIs.Page,
            UseCollectionView = true,
            Schema = tableMeta.Schema,
            Table = tableMeta.Name,
            Data = tableMeta.RealItemsName,
            Label = $"@{tableMeta.RealItemsName}",
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
                            Command = new FormItemCommand(FormCommand.Edit,
                                _metaProvider.GetOrAddEndpointPath(_dataSource, _meta)),
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

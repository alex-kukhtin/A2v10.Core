// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class FormBuilder
{
    private async Task<Form> CreateIndexFormAsync()
    {
        var tableMeta = await _metaProvider.GetSchemaAsync(_dataSource, _schema, _table);
        var appMeta = await _metaProvider.GetAppMetadataAsync(_dataSource);

        IEnumerable<FormItem> Columns() { 

            return VisibleColumns(tableMeta, appMeta).Select(
                c => new FormItem()
                {
                    Is = FormItemIs.DataGridColumn,
                    DataType = c.ToItemDataType(),
                    Data = c.IsReference ? $"{c.Name}.{appMeta.NameField}"  : c.Name,
                    Label = $"@[{c.Name}]"
                }
            ); 
        }

        IEnumerable<FormItem> ToolbarButtons()
        {
            yield return new FormItem(FormItemIs.Button)
            {
                Label = "@[Create]",
                Command = FormCommand.Create,
                Parameter = _metaProvider.GetOrAddEndpointPath(_dataSource, _schema, _table),
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Command = FormCommand.Edit,
                Parameter = _metaProvider.GetOrAddEndpointPath(_dataSource, _schema, _table),
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Command = FormCommand.Reload,
            };
            yield return new FormItem(FormItemIs.Aligner);
            yield return new FormItem(FormItemIs.TextBox)
            {
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
                    Label = $"@[{column.Name}]",
                    Data = $"Parent.Filter.{column.DataType}"
                };
            }

            var columns = tableMeta.Columns.Where(c => c.IsReference).Select(c => CreateFilter(c));

            return new FormItem(FormItemIs.Taskpad)
            {
                Items = [
                    new FormItem(FormItemIs.Panel) {
                        Label = "@[Filters]",
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
            Data = tableMeta.RealItemsName,
            Label = $"@[{tableMeta.RealItemsName}]",
            Items = [
                new FormItem() {
                    Is = FormItemIs.Grid,
                    Rows = "auto 1fr auto",
                    Height = "100%",
                    Items = [
                        new FormItem() {
                            Is = FormItemIs.Toolbar,
                            row = 1,
                            Items = [..ToolbarButtons()]
                        },
                        new FormItem() {
                            Is = FormItemIs.DataGrid,
                            Data = "Parent.ItemsSource",
                            row = 2,
                            Items = [..Columns()],
                            Command = FormCommand.Edit,
                            Parameter = _metaProvider.GetOrAddEndpointPath(_dataSource, _schema, _table),
                        },
                        new FormItem() {
                            Is = FormItemIs.Pager,
                            row = 3,
                            Data = "Parent.Pager"
                        }
                    ]
                }
            ],
            Taskpad = CreateTaskPad()
        };
    }
}

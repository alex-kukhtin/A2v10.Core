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
                    Data = c.IsReference ? $"{c.Name}.{appMeta.NameField}"  : c.Name,
                    Label = $"@[{c.Name}]"
                }
            ); 
        }

        IEnumerable<FormItem> ToolbarButtons()
        {
            yield return new FormItem()
            {
                Is = FormItemIs.Button,
                Command = FormCommand.Create,
                Label = "@[Create]",
                CommandParameter = _metaProvider.GetOrAddEndpointPath(_dataSource, _schema, _table),
            };
            yield return new FormItem()
            {
                Is = FormItemIs.Button,
                Command = FormCommand.Edit,
                CommandParameter = _metaProvider.GetOrAddEndpointPath(_dataSource, _schema, _table),
            };
            yield return new FormItem()
            {
                Is = FormItemIs.Button,
                Command = FormCommand.Reload,
            };
        }

        return new Form()
        {
            Is = FormItemIs.Page,
            UseCollectionView = true,
            Data = tableMeta.RealItemsName,
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
                            Items = [..Columns()]
                        },
                        new FormItem() {
                            Is = FormItemIs.Pager,
                            row = 3,
                            Data = "Parent.Pager"
                        }
                    ]
                }
            ]
        };
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class FormBuilder
{
    private async Task<Form> CreateBrowseDialogAsync()
    {
        var tableMeta = await _metaProvider.GetSchemaAsync(_dataSource, _schema, _table);
        var appMeta = await _metaProvider.GetAppMetadataAsync(_dataSource);

        IEnumerable<FormItem> Columns()
        {

            return VisibleColumns(tableMeta, appMeta).Select(
                c => new FormItem()
                {
                    Is = FormItemIs.DataGridColumn,
                    Data = c.IsReference ? $"{c.Name}.{appMeta.NameField}" : c.Name,
                    Label = $"@[{c.Name}]"
                }
            );
        }

        IEnumerable<FormItem> ToolbarButtons()
        {
            yield return new FormItem(FormItemIs.Button)
            {
                Command = FormCommand.Create,
                Label = "@[Create]",
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

        return new Form()
        {
            Is = FormItemIs.Dialog,
            Data = tableMeta.RealItemsName,
            UseCollectionView = true,
            Label = $"@[{tableMeta.RealItemsName}.Browse]",
            Items = [
                new FormItem()
                {
                    Is = FormItemIs.Grid,
                    Rows = "auto 1fr auto",
                    Items = [
                        new FormItem() {
                            Is = FormItemIs.Toolbar,
                            row = 1,
                            Items =  [..ToolbarButtons()]
                        },
                        new FormItem() {
                            Is = FormItemIs.DataGrid,
                            row = 2,
                            Height = "30rem",
                            Command = FormCommand.Select,
                            Parameter = tableMeta.RealItemsName,
                            Data = "Parent.ItemsSource",
                            Items = [..Columns()]
                        },
                        new FormItem() {
                            Is = FormItemIs.Pager,
                            row = 3,
                            Data = "Parent.Pager"
                        }
                    ]                        
                }
            ],
            Buttons = [
                new FormItem(FormItemIs.Button)
                {
                    Label = "@[Select]",
                    Command = FormCommand.Select,
                    Primary = true,
                    Parameter = tableMeta.RealItemsName
                },
                new FormItem(FormItemIs.Button)
                {
                    Label = "@[Cancel]",
                    Command = FormCommand.Close
                }
            ]
        };
    }
}

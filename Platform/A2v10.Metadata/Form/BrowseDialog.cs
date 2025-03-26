// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class FormBuilder
{
    private async Task<Form> CreateBrowseDialogAsync()
    {
        var tableMeta = await _metaProvider.GetSchemaAsync(_dataSource, _meta.Schema, _meta.Name);
        var appMeta = await _metaProvider.GetAppMetadataAsync(_dataSource);

        IEnumerable<FormItem> Columns()
        {

            return VisibleColumns(tableMeta, appMeta).Select(
                c => new FormItem()
                {
                    Is = FormItemIs.DataGridColumn,
                    Data = c.IsReference ? $"{c.Name}.{appMeta.NameField}" : c.Name,
                    Label = $"@{c.Name}"
                }
            );
        }

        IEnumerable<FormItem> ToolbarButtons()
        {
            yield return new FormItem(FormItemIs.Button)
            {
                Label = "@Create",
                Command = new FormItemCommand(FormCommand.Create)
                {
                    Url = _metaProvider.GetOrAddEndpointPath(_dataSource, _meta),
                    Argument = "Parent.ItemsSource"
                }
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.Edit)
                {
                    Url = _metaProvider.GetOrAddEndpointPath(_dataSource, _meta),
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
                Data = "Parent.Filter.Fragment",
                Width = "20rem"
            };
        }

        return new Form()
        {
            Is = FormItemIs.Dialog,
            Data = tableMeta.RealItemsName,
            UseCollectionView = true,
            Schema = tableMeta.Schema,
            Table = tableMeta.Name,
            Label = $"@{tableMeta.RealItemsName}.Browse",
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
                            Command = new FormItemCommand(FormCommand.Select, tableMeta.RealItemsName),
                            Data = "Parent.ItemsSource",
                            Items = [..Columns()]
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
                    Command = new FormItemCommand(FormCommand.Select, tableMeta.RealItemsName),
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

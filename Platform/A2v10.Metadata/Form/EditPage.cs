// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class FormBuilder
{
    private async Task<Form> CreateEditPageAsync()
    {
        var tableMeta = await _metaProvider.GetSchemaAsync(_dataSource, _meta.Schema, _meta.Name);
        var appMeta = await _metaProvider.GetAppMetadataAsync(_dataSource);


        IEnumerable<FormItem> ToolbarButtons()
        {
            yield return new FormItem(FormItemIs.Button)
            {
                Label = "@Save",
                Command = new FormItemCommand(FormCommand.Save)
            };
            yield return new FormItem(FormItemIs.Button)
            {
                Command = new FormItemCommand(FormCommand.Reload),
            };
            yield return new FormItem(FormItemIs.Aligner);
        }

        FormItem? CreateTaskPad()
        {
            return null;
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
                        }
                    ]
                }
            ],
            Taskpad = CreateTaskPad()
        };
    }
}

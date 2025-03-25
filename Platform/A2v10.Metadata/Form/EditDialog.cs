// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class FormBuilder
{
    private async Task<Form> CreateEditDialogAsync()
    {
        var tableMeta = await _metaProvider.GetSchemaAsync(_dataSource, _meta.Schema, _meta.Name);
        var appMeta = await _metaProvider.GetAppMetadataAsync(_dataSource);

        FormItem CreateControl(TableColumn column, Int32 index)
        {
            String? prm = null;
            if (column.IsReference)
                prm = _metaProvider.GetOrAddEndpointPath(_dataSource, column.Reference.RefSchema, column.Reference.RefTable);
            return new FormItem()
            {
                Is =  column.Column2Is(),
                Grid = new FormItemGrid(index, 1),
                Label = $"@{column.Name}",
                Data = $"{tableMeta.RealItemName}.{column.Name}",
                DataType = column.ToItemDataType(),
                Props = new FormItemProps()
                {
                    Url = prm
                },
                Width = column.DataType.ToWidth()
            };
        }

        IEnumerable<FormItem> Controls()
        {
            Int32 row = 1;
            return EditableColumns(tableMeta, appMeta).Select(c => CreateControl(c, row++));
        }

        return new Form()
        {
            Schema = tableMeta.Schema,
            Table = tableMeta.Name,
            Is = FormItemIs.Dialog,
            Items = [
                new FormItem(FormItemIs.Grid)
                {
                    Is = FormItemIs.Grid,
                    Props = new FormItemProps() 
                    {
                        Rows = String.Join(' ', EditableColumns(tableMeta, appMeta).Select(c => "auto")),
                        Columns = "1fr"
                    },
                    Items = [..Controls()]
                }
            ],
            Buttons = [
                new FormItem(FormItemIs.Button)
                {
                    Label = "@SaveAndClose",
                    Command = new FormItemCommand(FormCommand.SaveAndClose),
                    Props = new FormItemProps() {
                        Style = ItemStyle.Primary,
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

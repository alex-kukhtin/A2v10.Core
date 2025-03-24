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
        var tableMeta = await _metaProvider.GetSchemaAsync(_dataSource, _schema, _table);
        var appMeta = await _metaProvider.GetAppMetadataAsync(_dataSource);

        FormItem CreateControl(TableColumn column, Int32 index)
        {
            String? prm = null;
            if (column.IsReference)
                prm = _metaProvider.GetOrAddEndpointPath(_dataSource, column.Reference.RefSchema, column.Reference.RefTable);
            return new FormItem()
            {
                Is =  column.Column2Is(),
                row = index,
                Label = $"@[{column.Name}]",
                Data = $"{tableMeta.RealItemName}.{column.Name}",
                DataType = column.ToItemDataType(),
                Parameter = prm,
                Width = column.DataType.ToWidth()
            };
        }

        IEnumerable<FormItem> Controls()
        {
            Int32 row = 0;
            return VisibleColumns(tableMeta, appMeta).Select(c => CreateControl(c, row++));
        }

        return new Form()
        {
            Is = FormItemIs.Dialog,
            Items = [
                new FormItem(FormItemIs.Grid)
                {
                    Is = FormItemIs.Grid,
                    Items = [..Controls()]
                }
            ],
            Buttons = [
                new FormItem(FormItemIs.Button) 
                {
                    Label = "@[SaveAndClose]",
                    Command = FormCommand.SaveAndClose,
                    Primary = true,
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

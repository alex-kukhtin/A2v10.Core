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
            var fi = new FormItem()
            {
                Is = FormItemIs.TextBox,
                row = index,
                Label = $"@[{column.Name}]",
                Data = $"{tableMeta.RealItemName}.{column.Name}",
            };
            return fi;
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
                new FormItem()
                {
                    Is = FormItemIs.Grid,
                    Items = [..Controls()]
                }
            ]
        };
    }
}

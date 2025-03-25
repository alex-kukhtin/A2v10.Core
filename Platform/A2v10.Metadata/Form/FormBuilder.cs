// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class FormBuilder(DatabaseMetadataProvider _metaProvider, String? _dataSource, TableMetadata _meta)
{
    public async Task<Form> CreateFormAsync(String key)
    {
        return key.ToLowerInvariant() switch
        {
            "index" => await CreateIndexFormAsync(),
            "edit" => _meta.EditWith switch
            {
                EditWithMode.Dialog => await CreateEditDialogAsync(),
                EditWithMode.Page => await CreateEditPageAsync(),
                _ => throw new NotImplementedException($"CreateForm: {_meta.EditWith}")
            },
            "browse" => await CreateBrowseDialogAsync(),
            _ => throw new NotImplementedException($"CreateForm: {key}")
        };
    }

    public IEnumerable<TableColumn> VisibleColumns(TableMetadata table, AppMetadata appMeta)
    {
        Boolean IsVisible(String name)
        {
            return name != appMeta.VoidField
                && name != appMeta.IsFolderField 
                && name != appMeta.IsSystemField;
        }

        return table.Columns.Where(c => IsVisible(c.Name));
    }

    public IEnumerable<TableColumn> EditableColumns(TableMetadata table, AppMetadata appMeta)
    {
        Boolean IsVisible(String name)
        {
            return name != appMeta.VoidField
                && name != appMeta.IdField
                && name != appMeta.IsFolderField
                && name != appMeta.IsSystemField;
        }

        return table.Columns.Where(c => IsVisible(c.Name));
    }
}

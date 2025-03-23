// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal partial class FormBuilder(DatabaseMetadataProvider _metaProvider, String? _dataSource, String _schema, String _table)
{
    public async Task<Form> CreateFormAsync(String key)
    {
        return key.ToLowerInvariant() switch
        {
            "index" => await CreateIndexFormAsync(),
            "edit" => await CreateEditDialogAsync(),
            "browse" => await CreateBrowseDialogAsync(),
            _ => throw new NotImplementedException($"CreateForm: {key}")
        };
    }

    public IEnumerable<TableColumn> VisibleColumns(TableMetadata table, AppMetadata appMeta)
    {
        Boolean IsVisible(String name)
        {
            return name != appMeta.IdField && name != appMeta.VoidField
                && name != appMeta.IsFolderField && name != appMeta.IsSystemField;
        }

        return table.Columns.Where(c => IsVisible(c.Name));
    }
}

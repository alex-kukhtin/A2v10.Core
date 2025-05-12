// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    internal async Task<IEnumerable<ReferenceMember>> ReferenceFieldsAsync(TableMetadata table)
    {
        async Task<ReferenceMember> CreateMember(TableColumn column, Int32 index)
        {
            var table = column.DataType switch
            {
                ColumnDataType.Operation => MetadataExtensions.CreateOperationMeta(),
                ColumnDataType.Enum => MetadataExtensions.CreateEnumMeta(column),
                _ => await _metadataProvider.GetSchemaAsync(_dataSource, column.Reference.RefSchema, column.Reference.RefTable)
            };
            return new ReferenceMember(column, table, index);
        }
        Int32 index = 0;
        var list = new List<ReferenceMember>();
        foreach (var cx in table.Columns.Where(c => c.IsReference))
            list.Add(await CreateMember(cx, index++));
        return list;
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{

    internal async Task<IEnumerable<ReferenceMember>> ReferenceFieldsAsync(TableMetadata table)
    {
        TableMetadata CreateOperationMeta()
        {
            return new TableMetadata()
            {
                Schema = "op",
                Name = "Operations",
                Columns = new List<TableColumn>()
                {
                    new TableColumn()
                    {
                        Name = "Id",
                        DataType = ColumnDataType.String,
                        MaxLength = 16,
                        Role = TableColumnRole.PrimaryKey,
                    },
                    new TableColumn()
                    {
                        Name = "Name",
                        DataType = ColumnDataType.String,
                        MaxLength = 255,
                        Role = TableColumnRole.Name,
                    },
                },
            };
        }
        async Task<ReferenceMember> CreateMember(TableColumn column, Int32 index)
        {
            var table = column.DataType == ColumnDataType.Operation ?
                CreateOperationMeta()
                : await _metadataProvider.GetSchemaAsync(_dataSource, column.Reference.RefSchema, column.Reference.RefTable);
            return new ReferenceMember(column, table, index);
        }
        Int32 index = 0;
        var list = new List<ReferenceMember>();
        foreach (var cx in table.Columns.Where(c => c.IsReference))
            list.Add(await CreateMember(cx, index++));
        return list;
    }
}

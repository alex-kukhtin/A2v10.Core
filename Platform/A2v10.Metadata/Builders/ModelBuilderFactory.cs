// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class ModelBuilderFactory(
    IServiceProvider _serviceProvider,
    DatabaseMetadataProvider _metadataProvider) : IModelBuilderFactory
{
    public async Task<IModelBuilder> BuildAsync(IPlatformUrl platformUrl, IModelBase modelBase)
    {
        if (modelBase.Meta == null)
            throw new InvalidOperationException("Meta is null");

        var srcTable = await _metadataProvider.GetSchemaAsync(modelBase.Meta, modelBase.DataSource);
        var tables = await GetTablesAsync(modelBase.DataSource, srcTable);

        return new BaseModelBuilder(_serviceProvider) {
            _dataSource = modelBase.DataSource,
            _platformUrl = platformUrl,
            _table = tables.table,
            _baseTable = tables.baseTable,
            _refFields = await ReferenceFieldsAsync(modelBase.DataSource, tables.table),
            _appMeta = await _metadataProvider.GetAppMetadataAsync(modelBase.DataSource)
        };
    }
    public async Task<IModelBuilder> BuildAsync(IPlatformUrl platformUrl, TableMetadata table, String? dataSource)
    {
        var tables = await GetTablesAsync(dataSource, table);
        return new BaseModelBuilder(_serviceProvider)
        {
            _dataSource = dataSource,
            _platformUrl = platformUrl,
            _table = tables.table,
            _baseTable = tables.baseTable,
            _refFields = await ReferenceFieldsAsync(dataSource, tables.table),
            _appMeta = await _metadataProvider.GetAppMetadataAsync(dataSource)
        };
    }

    private async Task<(TableMetadata table, TableMetadata? baseTable)> GetTablesAsync(String? dataSource, TableMetadata table)
    {
        TableMetadata? baseTable = null;
        if (!table.ParentTable.IsEmpty())
        {
            baseTable = table;
            table = await _metadataProvider.GetSchemaAsync(dataSource, table.ParentTable!.RefSchema, table.ParentTable.RefTable)
                ?? throw new InvalidOperationException($"Parent Table {table.ParentTable.RefTable} not found");
        }
        return (table, baseTable);
    }

    internal async Task<IEnumerable<ReferenceMember>> ReferenceFieldsAsync(String? dataSource, TableMetadata table)
    {
        async Task<ReferenceMember> CreateMember(TableColumn column, Int32 index)
        {
            var table = column.DataType switch
            {
                ColumnDataType.Operation => MetadataExtensions.CreateOperationMeta(),
                ColumnDataType.Enum => MetadataExtensions.CreateEnumMeta(column),
                _ => await _metadataProvider.GetSchemaAsync(dataSource, column.Reference.RefSchema, column.Reference.RefTable)
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

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
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
        var (table, baseTable) = await GetTablesAsync(modelBase.DataSource, srcTable);

        var bd = new BuilderDescriptor()
        {
            DataSource = modelBase.DataSource,
            PlatformUrl = platformUrl,
            Table = table,
            BaseTable = baseTable,
            RefFields = await _metadataProvider.ReferenceFieldsAsync(modelBase.DataSource, table),
            AppMeta = await _metadataProvider.GetAppMetadataAsync(modelBase.DataSource)
        };

        return new BaseModelBuilder(_serviceProvider, bd);
    }
    public async Task<IModelBuilder> BuildAsync(IPlatformUrl platformUrl, TableMetadata table, String? dataSource)
    {
        var tables = await GetTablesAsync(dataSource, table);

        var bd = new BuilderDescriptor()
        {
            DataSource = dataSource,
            PlatformUrl = platformUrl,
            Table = tables.table,
            BaseTable = tables.baseTable,
            RefFields = await _metadataProvider.ReferenceFieldsAsync(dataSource, tables.table),
            AppMeta = await _metadataProvider.GetAppMetadataAsync(dataSource)
        };
        return new BaseModelBuilder(_serviceProvider, bd);
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
}

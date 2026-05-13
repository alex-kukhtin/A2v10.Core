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

        var bd = new BuilderDescriptor()
        {
            DataSource = modelBase.DataSource,
            PlatformUrl = platformUrl,
            Table = srcTable,
            RefFields = await _metadataProvider.ReferenceFieldsAsync(modelBase.DataSource, srcTable),
            AppMeta = await _metadataProvider.GetAppMetadataAsync(modelBase.DataSource)
        };

        return new BaseModelBuilder(_serviceProvider, bd);
    }
    public async Task<IModelBuilder> BuildAsync(IPlatformUrl platformUrl, TableMetadata table, String? dataSource)
    {
        var bd = new BuilderDescriptor()
        {
            DataSource = dataSource,
            PlatformUrl = platformUrl,
            Table = table,
            RefFields = await _metadataProvider.ReferenceFieldsAsync(dataSource, table),
            AppMeta = await _metadataProvider.GetAppMetadataAsync(dataSource)
        };
        return new BaseModelBuilder(_serviceProvider, bd);
    }

    private async Task<(TableMetadata table, TableMetadata? baseTable)> GetTablesAsync(String? dataSource, TableMetadata table)
    {
        TableMetadata? baseTable = null;
        if (!String.IsNullOrEmpty(table.Storage))
        {
            var (storageSchema, storageTable) = DatabaseMetadataProvider.ParsePath(table.Storage);
            baseTable = table;
            table.StorageTopTable = await _metadataProvider.GetSchemaAsync(dataSource, storageSchema, storageTable)
                ?? throw new InvalidOperationException($"Parent Table {table.Storage} not found");
        }
        return (table, baseTable);
    }
}

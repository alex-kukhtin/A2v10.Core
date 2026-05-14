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
        };
        return new BaseModelBuilder(_serviceProvider, bd);
    }

    public IEndpointModelBuilder BuildEndpoint(IPlatformUrl platformUrl, TableMetadata table, String? dataSource)
    {
        var bd = new BuilderDescriptor()
        {
            DataSource = dataSource,
            PlatformUrl = platformUrl,
            Table = table,
        };
        return new EndpointModelBuilder(_serviceProvider, bd);

    }
}

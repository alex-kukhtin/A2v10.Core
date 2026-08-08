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

        var endpoint = await _metadataProvider.GetEndpointAsync(modelBase.Meta, modelBase.DataSource) as NormalEndpointMetadata
            ?? throw new InvalidOperationException($"{modelBase.Meta.CurrentSchema}/{modelBase.Meta.CurrentTable} is not a data endpoint");

        var bd = new BuilderDescriptor()
        {
            DataSource = modelBase.DataSource,
            PlatformUrl = platformUrl,
            Endpoint = endpoint,
            PlatformId = await _metadataProvider.GetPlatformIdAsync(modelBase.DataSource),
        };

        return new BaseModelBuilder(_serviceProvider, bd);
    }
    public async Task<IModelBuilder> BuildAsync(IPlatformUrl platformUrl, NormalEndpointMetadata endpoint, String? dataSource)
    {
        var bd = new BuilderDescriptor()
        {
            DataSource = dataSource,
            PlatformUrl = platformUrl,
            Endpoint = endpoint,
            PlatformId = await _metadataProvider.GetPlatformIdAsync(dataSource),
        };
        return new BaseModelBuilder(_serviceProvider, bd);
    }

    public IEndpointModelBuilder BuildEndpoint(IPlatformUrl platformUrl, NormalEndpointMetadata endpoint, String? dataSource)
    {
        var bd = new BuilderDescriptor()
        {
            DataSource = dataSource,
            PlatformUrl = platformUrl,
            Endpoint = endpoint,
        };
        return new EndpointModelBuilder(bd);

    }
}

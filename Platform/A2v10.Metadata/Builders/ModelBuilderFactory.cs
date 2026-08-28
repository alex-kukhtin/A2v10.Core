// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class ModelBuilderFactory(
    IServiceProvider _serviceProvider,
    DatabaseMetadataProvider _metadataProvider) : IModelBuilderFactory
{
    /* The one place an endpoint becomes a builder, and it dispatches on the TYPE - no string
     * travels from the loader to here to say what this is. Every caller then holds one interface
     * and asks for what it wants; whether this endpoint serves that is the builder's own answer.
     * See CLAUDE.md, "System endpoints".
     */
    public async Task<IModelBuilder> BuildAsync(IPlatformUrl platformUrl, IModelBase modelBase)
    {
        if (modelBase.Meta == null)
            throw new InvalidOperationException("Meta is null");

        var dataSource = modelBase.DataSource;
        var endpoint = await _metadataProvider.GetEndpointAsync(modelBase.Meta, dataSource);
        var platformId = await _metadataProvider.GetPlatformIdAsync(dataSource);

        switch (endpoint)
        {
            case ReportEndpointMetadata report:
                return new ReportEndpointBuilder(_serviceProvider, report, platformUrl, platformId);
            case TagEndpointMetadata tag:
                return new TagEndpointBuilder(_serviceProvider, tag, platformUrl, dataSource, platformId);
            case OperationEndpointMetadata operation:
                return new OperationEndpointBuilder(_serviceProvider, operation, platformUrl, dataSource);
            case NormalEndpointMetadata normal:
                return new BaseModelBuilder(_serviceProvider, new BuilderDescriptor()
                {
                    DataSource = dataSource,
                    PlatformUrl = platformUrl,
                    Endpoint = normal,
                    PlatformId = platformId,
                });
            default:
                throw new InvalidOperationException($"No builder for endpoint '{endpoint.Path}'");
        }
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

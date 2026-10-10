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
        // the endpoint is the folder the request names; model.json never repeats the address
        var dataSource = modelBase.DataSource;
        var (schema, table) = DatabaseMetadataProvider.ParsePath(platformUrl.LocalPath);
        var endpoint = await _metadataProvider.GetEndpointAsync(dataSource, schema, table);
        var platformId = await _metadataProvider.GetPlatformIdAsync(dataSource);
        var useGrants = await _metadataProvider.UseGrantsAsync();

        // the namespace's one right, before any of its screens is built - see AdminEndpointMetadata
        if (endpoint is AdminEndpointMetadata)
            await AdminGate.CheckAsync(_serviceProvider, dataSource);

        switch (endpoint)
        {
            case ReportEndpointMetadata report:
                return new ReportEndpointBuilder(_serviceProvider, report, platformUrl, platformId, useGrants);
            case TagEndpointMetadata tag:
                return new TagEndpointBuilder(_serviceProvider, tag, platformUrl, dataSource, platformId);
            case OperationEndpointMetadata operation:
                return new OperationEndpointBuilder(_serviceProvider, operation, platformUrl, dataSource);
            case UserAdminEndpointMetadata user:
                return new UserAdminBuilder(_serviceProvider, user, platformUrl, dataSource, platformId);
            case NormalEndpointMetadata normal:
                return new BaseModelBuilder(_serviceProvider, new BuilderDescriptor()
                {
                    DataSource = dataSource,
                    PlatformUrl = platformUrl,
                    Endpoint = normal,
                    PlatformId = platformId,
                    UseGrants = useGrants,
                });
            default:
                throw new InvalidOperationException($"No builder for endpoint '{endpoint.Path}'");
        }
    }
}

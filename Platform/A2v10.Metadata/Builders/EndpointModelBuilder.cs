// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal class EndpointModelBuilder(IServiceProvider _serviceProvider, BuilderDescriptor descriptor) : IEndpointModelBuilder
{
    private readonly TypescriptBuilder _tsBuilder = new(descriptor);
    protected String Action => descriptor.PlatformUrl.Action.ToLowerInvariant();
    public async Task<String> CreateTemplateTSAsync()
    {
        return Action switch
        {
            "index" => await _tsBuilder.CreateIndexTSTemplate(),
            "edit" => await _tsBuilder.CreateEditTSTemplate(),
            "browse" => String.Empty,
            "browsefolder" => String.Empty,
            _ => throw new NotImplementedException($"Create ts template for {Action}")
        };
    }

    public async Task<String> CreateMapTSAsync()
    {
        return Action switch
        {
            "index" => await _tsBuilder.CreateIndexMapTS(),
            "edit" => await _tsBuilder.CreateEditMapTS(),
            "browse" => String.Empty,
            "browsefolder" => String.Empty,
            _ => throw new NotImplementedException($"Create ts template for {Action}")
        };
    }

}

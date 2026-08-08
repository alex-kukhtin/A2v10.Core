// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal class EndpointModelBuilder(BuilderDescriptor descriptor) : IEndpointModelBuilder
{
    // the same emitter as the runtime uses, printing the types this time
    private readonly ScriptBuilder _tsBuilder = new(descriptor, isTs: true);
    protected String Action => descriptor.PlatformUrl.Action.ToLowerInvariant();
    public async Task<String> CreateTemplateTSAsync()
    {
        return Action switch
        {
            "index" => await _tsBuilder.CreateIndexTemplate(),
            "edit" => await _tsBuilder.CreateEditTemplate(),
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

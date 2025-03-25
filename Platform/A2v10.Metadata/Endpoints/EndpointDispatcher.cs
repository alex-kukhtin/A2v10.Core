// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

public static class EndpointDispatcher
{
    public static IEndpointHandler FindHandler(IServiceProvider serviceProvider, String name)
    {
        return name.ToLowerInvariant() switch
        {
            "meta:form.edit" => new EditFormEndpointHandler(serviceProvider),
            _ => throw new NotImplementedException($"Endpoint handler not found. ({name})")
        };
    }
}

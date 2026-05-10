// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

public class MetadataEndpointHandler(IServiceProvider _serviceProvider) : IEndpointHandler
{
    public Task<String> RenderResultAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
    {
        var endpontName = modelView.EndpointHandler
            ?? throw new InvalidOperationException("EndpointHandler is null");
        return EndpointDispatcher.FindHandler(_serviceProvider, endpontName).RenderResultAsync(platformUrl, modelView, prms);
    }
    public Task<IDataModel> ReloadAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject prms)
    {
        var endpontName = modelView.EndpointHandler
            ?? throw new InvalidOperationException("EndpointHandler is null");
        return EndpointDispatcher.FindHandler(_serviceProvider, endpontName).ReloadAsync(platformUrl, modelView, prms);
    }

    public Task<ExpandoObject> SaveAsync(IPlatformUrl platformUrl, IModelView modelView, ExpandoObject data, ExpandoObject prms)
    {
        var endpontName = modelView.EndpointHandler
            ?? throw new InvalidOperationException("EndpointHandler is null");
        return EndpointDispatcher.FindHandler(_serviceProvider, endpontName).SaveAsync(platformUrl, modelView, data, prms);
    }
}

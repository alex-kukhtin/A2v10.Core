// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.System.Xaml;
using System;

namespace A2v10.Metadata;

// the endpoint and the url, nothing of the builder's: no data source, no base, no grants reach a page
internal partial class XamlBuilder(NormalEndpointMetadata endpoint, IPlatformUrl platformUrl)
{
    private readonly NormalEndpointMetadata Endpoint = endpoint;
    private readonly TableMetadata Table = endpoint.Storage;
    private readonly DeclarationMetadata Declaration = endpoint.Declaration;
    protected readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();
}

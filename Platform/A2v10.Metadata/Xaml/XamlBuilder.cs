// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.System.Xaml;
using System;

namespace A2v10.Metadata;

internal partial class XamlBuilder(BuilderDescriptor desciptor)
{
    private readonly NormalEndpointMetadata Endpoint = desciptor.Endpoint;
    private readonly TableMetadata Table = desciptor.Endpoint.Storage;
    private readonly DeclarationMetadata Declaration = desciptor.Endpoint.Declaration;
    protected readonly IServiceProvider _xamlServiceProvider = new XamlServiceProvider();
}

// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal record BuilderDescriptor
{
    public NormalEndpointMetadata Endpoint { get; init; } = default!;
    // the shape is always the endpoint's own - there is no way to pair a descriptor
    // with a table that belongs to some other endpoint
    public TableMetadata Table => Endpoint.Storage;
    internal String? DataSource { get; init; }
    internal IPlatformUrl PlatformUrl { get; init; } = default!;
    internal AppPlatformId PlatformId { get; init; } = default!;
}

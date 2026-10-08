// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal record BuilderDescriptor
{
    public NormalEndpointMetadata Endpoint { get; init; } = default!;
    internal String? DataSource { get; init; }
    internal IPlatformUrl PlatformUrl { get; init; } = default!;
    internal AppPlatformId PlatformId { get; init; } = default!;
    // app.json useGrants: whether the batches carry the gate. Required - a descriptor that forgot it would open them all
    internal required Boolean UseGrants { get; init; }
}

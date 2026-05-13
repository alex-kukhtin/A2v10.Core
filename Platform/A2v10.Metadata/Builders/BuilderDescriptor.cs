// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal record BuilderDescriptor
{
    public TableMetadata Table { get; init; } = default!;
    internal String? DataSource { get; init; }
    internal IPlatformUrl PlatformUrl { get; init; } = default!;
}

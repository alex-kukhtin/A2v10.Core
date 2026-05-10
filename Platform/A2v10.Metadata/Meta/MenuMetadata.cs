// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

public record MenuItem
{
    public String Title { get; init; } = default!;
    public String? Icon { get; init; }
    public String? Url { get; init; }
    public String? Category { get; init; }
    public MenuItem[]? Items { get; init; }
}

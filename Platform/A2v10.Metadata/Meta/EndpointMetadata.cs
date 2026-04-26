// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal enum FieldType
{
    Undefined,
    // Sql Types
    String,
    Char,
    Int,
    Id,
    Boolean,
    Date,
    DateTime,
    Money,
    // Domain Based
    Qty,
    Ref,
    Code,
    EMail,
    Phone,
    Url,
    Memo
}

internal record FieldMetadata
{
    public String Name { get; init; } = default!;
    public FieldType Type { get; init; } // Domain Based
    public String? Target { get; init; } // Required for Ref;
    public Int32? Length { get; init; }  // Required for String
    public Boolean Required { get; init; }
}

internal class EndpointMetadata
{
    public String Schema { get; init; } = default!;
    public String Table { get; init; } = default!;
    public String Model { get; init; } = default!;
    public Dictionary<String, FieldMetadata> Fields { get; init; } = [];
}

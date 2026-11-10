// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Metadata;

/* The 'mcp' key of metadata.json: only what derives wrong for the model. Read by the index from the
 * address's own file (McpIndex), never by the endpoint load - so it is per address by construction and
 * nothing layers it from 'storage'.
 * See CLAUDE.md, "MCP: the entities the model sees".
 */
public sealed record McpDeclarationMetadata
{
    public String? Description { get; init; }
    // a report: measure -> what it is when the name and the type do not say (Sum: the cost or the revenue)
    public Dictionary<String, String>? Measures { get; init; }
}

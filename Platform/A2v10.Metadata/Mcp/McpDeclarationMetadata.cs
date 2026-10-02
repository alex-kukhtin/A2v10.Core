// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Metadata;

public enum McpSearchMode
{
    Exact,
    Contains
}

/* The 'mcp' key of metadata.json: only what derives wrong for the model. Read by the index from the
 * address's own file (McpIndex), never by the endpoint load - so it is per address by construction and
 * nothing layers it from 'storage'. 'SearchBy', written, REPLACES the derived set whole.
 * See CLAUDE.md, "MCP: the entities the model sees".
 */
public sealed record McpDeclarationMetadata
{
    public String? Description { get; init; }
    public Dictionary<String, McpSearchMode>? SearchBy { get; init; }
}

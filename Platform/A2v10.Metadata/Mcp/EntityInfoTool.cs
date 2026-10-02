// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

// the projection of one entity; its endpoint is loaded here, at the first call, and cached with the metadata
internal sealed class EntityInfoTool(IReadOnlyList<McpEntity> entities, DatabaseMetadataProvider metadata) : IPlatformMcpTool
{
    public String Name => "entity_info";
    public String Title => "Entity fields";
    public PlatformMcpToolHints Hints => PlatformMcpToolHints.ReadOnly;
    public String Description => """
        What an entity is: its fields, and for a document its collections of rows (one collection per kind of row).
        A field has a type and may be: required; computed - the platform computes it, never send it; readOnly - written by the platform, never send it; default - what fills it when it is not sent.
        A reference to a catalog names it in 'catalog': find the id with catalog_find. 'unavailable' - a reference you cannot fill.
        On a catalog, 'search' marks the fields catalog_find matches: 'contains' - a substring, 'exact' - the whole value.
        An enum lists its values: send the id.
        """;
    public JsonElement InputSchema => McpArgs.Schema(new
    {
        type = "object",
        properties = new
        {
            entity = new { type = "string", description = "An entity name from domain_info", @enum = entities.Select(e => e.Name) }
        },
        required = new[] { "entity" },
        additionalProperties = false
    });
    public String[]? Roles => null;

    public async Task<Object?> ExecuteAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var entity = McpArgs.Entity(entities, McpArgs.Text(args, "entity"), "entity");
        var endpoint = await metadata.GetNormalEndpointAsync(null, entity.Schema, entity.Table);
        return McpProjection.Describe(endpoint, entity, path => entities.FirstOrDefault(e => e.IsCatalog && e.Path == path)?.Name);
    }
}

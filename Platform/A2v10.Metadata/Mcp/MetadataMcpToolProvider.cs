// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The tools of the metadata layer: domain_info, entity_info, catalog_find. The host registers it with
 * services.UseMcp(Configuration, tools => tools.AddProvider<MetadataMcpToolProvider>()).
 * The tools are created here, per request, for this user: the entities their roles reach are the enums
 * of the schemas. No entity reached - no tool; no catalog reached - no catalog_find.
 */
public sealed class MetadataMcpToolProvider(DatabaseMetadataProvider metadata, ICurrentUser currentUser, IDbContext dbContext,
    ILocalizer localizer) : IPlatformMcpToolProvider
{
    public async Task<IEnumerable<IPlatformMcpTool>> GetToolsAsync()
    {
        var index = await metadata.GetMcpIndexAsync();
        var visible = index.Entities.Where(e => e.IsVisibleTo(currentUser.Identity.Roles)).ToList();
        if (visible.Count == 0)
            return [];
        List<IPlatformMcpTool> tools = [new DomainInfoTool(visible, metadata, localizer), new EntityInfoTool(visible, metadata)];
        var catalogs = visible.Where(e => e.IsCatalog).ToList();
        if (catalogs.Count > 0)
            tools.Add(new CatalogFindTool(catalogs, metadata, currentUser, dbContext));
        return tools;
    }
}

internal static class McpArgs
{
    internal static JsonElement Schema(Object schema) => JsonSerializer.SerializeToElement(schema);

    internal static String Text(JsonElement args, String name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new InvalidOperationException($"'{name}' is required: a string");

    // hidden by roles answers as absent: the difference would reveal the entity exists
    internal static McpEntity Entity(IReadOnlyList<McpEntity> entities, String name, String what) =>
        entities.FirstOrDefault(e => e.Name == name)
            ?? throw new InvalidOperationException($"Unknown {what} '{name}'. Known: {String.Join(", ", entities.Select(e => e.Name))}");
}

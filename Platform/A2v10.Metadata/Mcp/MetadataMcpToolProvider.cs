// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The tools of the metadata layer: domain_info and the families catalog_*, document_*. The host registers it with
 * services.UseMcp(Configuration, tools => tools.AddProvider<MetadataMcpToolProvider>()).
 * The tools are created here, per request, for this user: the entities their roles reach are the enums
 * of the schemas. No entity reached - no tool; no entity of a kind reached - no family of that kind.
 */
public sealed class MetadataMcpToolProvider(DatabaseMetadataProvider metadata, ICurrentUser currentUser, IDataService dataService,
    ILocalizer localizer) : IPlatformMcpToolProvider
{
    public async Task<IEnumerable<IPlatformMcpTool>> GetToolsAsync()
    {
        var index = await metadata.GetMcpIndexAsync();
        var visible = index.Entities.Where(e => e.IsVisibleTo(currentUser.Identity.Roles)).ToList();
        if (visible.Count == 0)
            return [];
        List<IPlatformMcpTool> tools = [new DomainInfoTool(visible, metadata, localizer)];
        foreach (var family in visible.GroupBy(e => e.Kind))
        {
            var entities = family.ToList();
            tools.Add(new RecordInfoTool(family.Key, entities, visible, metadata));
            tools.Add(new RecordQueryTool(family.Key, entities, metadata, dataService));
            tools.Add(new RecordLoadTool(family.Key, entities, metadata, dataService));
        }
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

    // a value of the query: a string or a number, as a URL would carry it
    internal static String Scalar(JsonElement value, String name) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number => value.GetRawText(),
        _ => throw new InvalidOperationException($"'{name}' is {value.ValueKind}: a string or a number is expected")
    };

    // hidden by roles answers as absent: the difference would reveal the entity exists
    internal static McpEntity Entity(IReadOnlyList<McpEntity> entities, String name, String what) =>
        entities.FirstOrDefault(e => e.Name == name)
            ?? throw new InvalidOperationException($"Unknown {what} '{name}'. Known: {String.Join(", ", entities.Select(e => e.Name))}");
}

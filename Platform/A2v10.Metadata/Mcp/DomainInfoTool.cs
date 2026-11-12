// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

// name is what document_create takes, as '?Op=' does on a screen; title is what the user calls it
internal sealed record McpOperation(String Name, String Title);

internal sealed record McpDomainEntity(String Name, String Kind,
    String Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<McpOperation>? Operations,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] String? Description);

/* The glossary, per user: which word means which entity. The words are the screens' own
 * (MetadataExtensions.OperationLabel, RecordLabel) in the user's locale - what the user calls the thing.
 * The endpoints are loaded here: the label is spelled from the shape (Model, the operations), and a second
 * spelling of it in the index would drift from the screen's.
 */
internal sealed class DomainInfoTool(IReadOnlyList<McpEntity> entities, DatabaseMetadataProvider metadata, ILocalizer localizer)
    : IPlatformMcpTool
{
    public String Name => "domain_info";
    public String Title => "Application entities";
    public PlatformMcpToolHints Hints => PlatformMcpToolHints.ReadOnly;
    public String Description => """
        The entities of this application that the current user can reach: name, kind (catalog, document or report), title - what the user calls it; operations - the kinds of a document: the user names the title, document_create takes the name; description when the name is not enough.
        Call once at the start of a task. A catalog's name is what the catalog_* tools take, a document's - the document_* tools, a report's - the report_* tools.
        """;
    public JsonElement InputSchema => McpArgs.Schema(new { type = "object", properties = new { }, additionalProperties = false });
    public String[]? Roles => null;

    public async Task<Object?> ExecuteAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var list = new List<McpDomainEntity>();
        foreach (var e in entities)
        {
            if (e.IsReport)
            {
                list.Add(new McpDomainEntity(e.Name, e.Kind, Label(MetadataExtensions.ReportLabel(e.Table)), null, e.Mcp?.Description));
                continue;
            }
            var endpoint = await metadata.GetNormalEndpointAsync(null, e.Schema, e.Table);
            var operations = endpoint.Declaration.OperationDeclarations
                .Select(o => new McpOperation(o.Name, Label(MetadataExtensions.OperationLabel(o.Id))))
                .ToList();
            list.Add(new McpDomainEntity(e.Name, e.Kind,
                Label(e.IsCatalog ? endpoint.Storage.RecordLabel() : MetadataExtensions.OperationLabel(endpoint.Name)),
                operations.Count > 0 ? operations : null,
                e.Mcp?.Description));
        }
        return new { Entities = list };
    }

    /* As the screen shows it: a key without a translation comes back as itself ('@Operation.order') and
     * is kept - a missing translation stays visible here as it is on the page, never silently dropped.
     */
    String Label(String key) => localizer.Localize(key) ?? key;
}

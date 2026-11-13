// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Infrastructure;
using A2v10.Services.Api;

namespace A2v10.Metadata;

// named as a record's own fields are, not as the answer's keys: the model sends it back as a reference, { "Id": code }
internal sealed record McpCode([property: JsonPropertyName("Id")] String Id, [property: JsonPropertyName("Name")] String? Name);

/* The codes of a codeset as its picker offers them (DataAt.BrowseAsync): through the door, so the gate is the builder's,
 * and the closed codes are no candidates. A chart is hundreds of rows read whole (SqlBuilder.LoadAccountTreeModelAsync),
 * so the page and the Fragment are cut here, over the tree laid flat in its own order - a parent before its children.
 * Fragment matches the code and the name in one string, as DisplayName does on a screen.
 */
internal sealed class CodesetQueryTool(IReadOnlyList<McpEntity> codesets, DatabaseMetadataProvider metadata, IDataService dataService,
    ILocalizer localizer) : IPlatformMcpTool
{
    const Int32 DefaultTake = 20;

    public String Name => "codeset_query";
    public String Title => "List codes";
    public PlatformMcpToolHints Hints => PlatformMcpToolHints.ReadOnly;
    public String Description => """
        Lists the codes of a codeset - a chart of accounts: what a field or a report dimension that names a 'codeset' takes. A code is the Id: a field takes { "Id": code }, a filter of report_query the code itself.
        filter: Fragment - a substring of the code or of the name. No condition - all the codes, and 'total' says how many.
        The answer: rows - one page, Id and Name, in the order of the codeset, a parent before the codes under it; total - all that matched; filter, take, skip - what was applied.
        """;
    public JsonElement InputSchema => RecordTool.Schema(McpEntity.Codeset, codesets, new
    {
        filter = new { type = "object", description = "The conditions, in the form the answer's 'filter' has" },
        take = new { type = "integer", description = "The size of a page; by default 20" },
        skip = new { type = "integer", description = "Rows to skip" }
    });
    public String[]? Roles => null;

    public async Task<Object?> ExecuteAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var entity = McpArgs.Entity(codesets, McpArgs.Text(args, McpEntity.Codeset), McpEntity.Codeset);
        var table = (await metadata.GetNormalEndpointAsync(null, entity.Schema, entity.Table)).Storage;
        var filter = RecordQueryTool.Conditions(args, table, [Constants.QueryNames.Fragment]);
        var fragment = filter.Get<String>(Constants.QueryNames.Fragment);
        var skip = RecordQueryTool.Number(args, "skip");
        var take = RecordQueryTool.Number(args, "take") is > 0 and var size ? size : DefaultTake;

        var result = await new EndpointDataService(dataService).At(entity.Path.Trim('/')).BrowseAsync();
        var codes = Flat(result.Data?.Get<IEnumerable<Object>>(table.CollectionName))
            .Select(node => Code(node))
            .Where(c => fragment == null || $"{c.Id} {c.Name}".Contains(fragment, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return new
        {
            Rows = codes.Skip(skip).Take(take).ToList(),
            Total = codes.Count,
            Filter = filter,
            Take = take,
            Skip = skip
        };
    }

    static IEnumerable<ExpandoObject> Flat(IEnumerable<Object>? nodes) =>
        (nodes ?? []).OfType<ExpandoObject>().SelectMany(node => Flat(node.Get<IEnumerable<Object>>(Constants.FieldNames.Items)).Prepend(node));

    // a name may be a key, as the screen shows it
    McpCode Code(ExpandoObject node)
    {
        var name = node.Get<String>(Constants.FieldNames.Name);
        return new McpCode($"{node.Get<Object>(Constants.FieldNames.Id)}", localizer.Localize(name) ?? name);
    }
}

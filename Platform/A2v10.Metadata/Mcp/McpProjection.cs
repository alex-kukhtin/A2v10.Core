// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace A2v10.Metadata;

internal sealed record McpSetValue(String Id, String? Name);

/* One field of a record as catalog_info and document_info show it. Optional members are absent, never null or false:
 * the model reads every key it is given.
 */
internal sealed record McpField(String Name, String Type)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Boolean? Required { get; init; }
    // the database computes it: nothing the model sends reaches it
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Boolean? Computed { get; init; }
    // the expression a screen computes it by: it runs in the browser, and the model has none, so it computes and sends the value
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public String? Get { get; init; }
    // written by the platform or fixed by the address: not sent on a create, back as it came on a save
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Boolean? ReadOnly { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public String? Default { get; init; }
    // a reference: its target's name under the key of its kind, which is the parameter of that family's tools
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public String? Catalog { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public String? Document { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public String? Codeset { get; init; }
    // a reference the model cannot fill: not a listed entity this user reaches
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Boolean? Unavailable { get; init; }
    // an enum or a state: the closed set inline; the value is sent as the code
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<McpSetValue>? Values { get; init; }
}

internal sealed record McpCollection(String Name, IReadOnlyList<McpField> Fields);

// one filter *_query takes: the name is the key under its 'filter'
internal sealed record McpFilter(String Name, String Kind);

internal sealed record McpEntityInfo(String Entity, String Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] String? Description,
    IReadOnlyList<McpField> Fields, IReadOnlyList<McpCollection> Collections, IReadOnlyList<McpFilter> Filters,
    // the listed documents this user reaches that can be created from this one; what is hidden is not named
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<String>? BasedOn = null);

/* What a record is for the model (catalog_info, document_info). Every column but TableColumn.IsMcpColumn's
 * exclusions, named as in the shape; not the form. A collection with kinds is one collection per kind, under its
 * composed name (StockRows), each with its own rules - the model sets no Kind.
 */
internal static class McpProjection
{
    internal static String TypeName(TableColumn column) => JsonNamingPolicy.CamelCase.ConvertName(column.Type.ToString());

    // the listed entity a reference points at, if this user reaches it - by a record's field and by a report's dimension alike
    internal static McpEntity? TargetOf(TableColumn column, Func<String, McpEntity?> entityOf) =>
        column.RefTable is NormalEndpointMetadata ep ? entityOf(ep.Path) : null;

    // the living values of a set, without the filter's 'All' row (its key is the empty string)
    internal static IReadOnlyList<McpSetValue>? SetValues(TableColumn column) => column.IsSetRef
        ? [.. column.RefTableCheck.Storage.Values.Where(v => !v.Void && v.Id.Length > 0).Select(v => new McpSetValue(v.Id, v.Name))]
        : null;

    // entityOf: the listed entity this user reaches, by its path; null otherwise
    internal static McpEntityInfo Describe(NormalEndpointMetadata endpoint, McpEntity entity, Func<String, McpEntity?> entityOf)
    {
        var declaration = endpoint.Declaration;
        var initials = endpoint.AllInitials();
        var fields = endpoint.Storage.AllColumns(c => c.IsMcpColumn)
            .Select(c => Field(c, declaration.Rules, declaration.Properties, declaration, initials, header: true, entityOf));
        var collections = endpoint.Storage.Details.SelectMany(d => declaration.Details[d.Key].RowSets
            .Select(rs => new McpCollection(rs.Collection, [.. d.Value.AllColumns(c => c.IsMcpColumn)
                .Select(c => Field(c, rs.Rules, rs.Properties, declaration, null, header: false, entityOf))])));
        var filters = endpoint.Storage.Filters(declaration)
            .Select(f => new McpFilter(f.Name, JsonNamingPolicy.CamelCase.ConvertName(f.Kind.ToString())));
        List<String> births = [.. declaration.BasedOn.Select(b => entityOf(b.Target)).OfType<McpEntity>().Select(e => e.Name)];
        return new McpEntityInfo(entity.Name, entity.Kind, entity.Mcp?.Description, [.. fields], [.. collections], [.. filters],
            births.Count > 0 ? births : null);
    }

    static McpField Field(TableColumn column, RuleMetadata rules, IReadOnlyDictionary<String, PropertyMetadata> properties,
        DeclarationMetadata declaration, IReadOnlyDictionary<String, InitialMetadata>? initials, Boolean header,
        Func<String, McpEntity?> entityOf)
    {
        var readOnly = IsReadOnly(column, declaration, header);
        McpEntity? target = null;
        Boolean? unavailable = null;
        if (column.IsRef && !column.IsSetRef && !readOnly)
        {
            target = TargetOf(column, entityOf);
            unavailable = target == null ? true : null;
        }
        return new McpField(column.Name, TypeName(column))
        {
            Required = rules.Required.Contains(column.Name) ? true : null,
            Computed = column.HasSqlAs ? true : null,
            Get = properties.TryGetValue(column.Name, out var property) ? property.Get : null,
            ReadOnly = readOnly ? true : null,
            Default = Default(column, rules, initials),
            Catalog = target is { IsCatalog: true } ? target.Name : null,
            Document = target is { IsDocument: true } ? target.Name : null,
            Codeset = target is { IsCodeset: true } ? target.Name : null,
            Unavailable = unavailable,
            Values = SetValues(column)
        };
    }

    /* Written by the platform or fixed by the address. An issued number is the platform's; a number
     * without a numbering is typed by hand. A basis in the header is set by birth, in a row it is picked.
     * A state moves by its transitions, so offering every value would be the picker's lie.
     */
    static Boolean IsReadOnly(TableColumn column, DeclarationMetadata declaration, Boolean header) =>
        column.Type is ColumnType.Id or ColumnType.Done or ColumnType.IsSystem or ColumnType.Operation
            or ColumnType.State or ColumnType.RowNumber
        || column.Type == ColumnType.Autonum && !String.IsNullOrEmpty(declaration.Autonum)
        || header && (column.Type == ColumnType.BasedOn || declaration.Fixed.ContainsKey(column.Name));

    // what fills an empty field: a literal, 'today', where it is inherited from
    static String? Default(TableColumn column, RuleMetadata rules, IReadOnlyDictionary<String, InitialMetadata>? initials)
    {
        if (initials != null && initials.TryGetValue(column.Name, out var initial))
            return initial.Source switch
            {
                InitialSource.Literal or InitialSource.Context => initial.Value,
                InitialSource.Query => null, // chosen by the url that opens creation
                _ => JsonNamingPolicy.CamelCase.ConvertName(initial.Source.ToString())
            };
        if (rules.Inherit.TryGetValue(column.Name, out var inherit))
            return $"from {inherit.Ref}.{inherit.Field}";
        return null;
    }
}

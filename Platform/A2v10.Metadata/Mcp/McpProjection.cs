// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace A2v10.Metadata;

internal sealed record McpSetValue(String Id, String? Name);

/* One field of a record as entity_info shows it. Optional members are absent, never null or false:
 * the model reads every key it is given.
 */
internal sealed record McpField(String Name, String Type)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Boolean? Required { get; init; }
    // the platform computes it: the model sends qty and price, never the sum
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Boolean? Computed { get; init; }
    // written by the platform or fixed by the address: the model reads it and never sends it
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Boolean? ReadOnly { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public String? Default { get; init; }
    // a reference to a catalog: its name, which is catalog_find's parameter as it stands
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public String? Catalog { get; init; }
    // a reference the model cannot fill: not a listed catalog this user reaches - a document included,
    // since nothing finds a document's id yet
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Boolean? Unavailable { get; init; }
    // a catalog's field catalog_find matches: 'exact' or 'contains'
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public String? Search { get; init; }
    // an enum or a state: the closed set inline; the value is sent as the code
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<McpSetValue>? Values { get; init; }
}

internal sealed record McpCollection(String Name, IReadOnlyList<McpField> Fields);

internal sealed record McpEntityInfo(String Entity, String Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] String? Description,
    IReadOnlyList<McpField> Fields, IReadOnlyList<McpCollection> Collections);

/* What a record is for the model - one projection, three readers (entity_info describes it, catalog_find
 * returns rows in it, entity_crud will accept it). Every column but TableColumn.IsMcpColumn's exclusions,
 * named as in the shape; not the form. A collection with kinds is one collection per kind, under its
 * composed name (StockRows), each with its own rules - the model sets no Kind.
 */
internal static class McpProjection
{
    internal static String TypeName(TableColumn column) => JsonNamingPolicy.CamelCase.ConvertName(column.Type.ToString());

    // catalogOf: the name of a listed catalog this user reaches, by its path; null otherwise
    internal static McpEntityInfo Describe(NormalEndpointMetadata endpoint, McpEntity entity, Func<String, String?> catalogOf)
    {
        var declaration = endpoint.Declaration;
        var initials = endpoint.AllInitials();
        var search = entity.IsCatalog
            ? SearchSet(endpoint, entity).ToDictionary(s => s.Column.Name, s => JsonNamingPolicy.CamelCase.ConvertName(s.Mode.ToString()))
            : [];
        var fields = endpoint.Storage.AllColumns(c => c.IsMcpColumn)
            .Select(c => Field(c, declaration.Rules, declaration, initials, header: true, catalogOf)
                with { Search = search.GetValueOrDefault(c.Name) });
        var collections = endpoint.Storage.Details.SelectMany(d => declaration.Details[d.Key].RowSets
            .Select(rs => new McpCollection(rs.Collection, [.. d.Value.AllColumns(c => c.IsMcpColumn)
                .Select(c => Field(c, rs.Rules, declaration, null, header: false, catalogOf))])));
        return new McpEntityInfo(entity.Name, entity.Kind, entity.Mcp?.Description, [.. fields], [.. collections]);
    }

    /* What catalog_find matches a catalog by - and entity_info marks it, so the model knows which text finds what.
     * Written: 'mcp.searchBy' of the address, whole. Derived: the column a record is chosen by (displayAs) as a substring - finding is choosing - a declared
     * unique string field exactly - a code that is not unique is a bad exact key anyway.
     */
    internal static IEnumerable<(TableColumn Column, McpSearchMode Mode)> SearchSet(NormalEndpointMetadata endpoint, McpEntity catalog)
    {
        var table = endpoint.Storage;
        if (catalog.Mcp?.SearchBy is { } written)
            return written.Select(kp => (table.AllColumns().FirstOrDefault(c => c.Name == kp.Key)
                ?? throw new InvalidOperationException($"{catalog.Path}: mcp.searchBy names '{kp.Key}', which is not a field of it"), kp.Value));
        return table.AllColumns(c => c.Name == table.DisplayAs || c.Unique && c.Type is ColumnType.String or ColumnType.Name)
            .Select(c => (c, c.Name == table.DisplayAs ? McpSearchMode.Contains : McpSearchMode.Exact));
    }

    static McpField Field(TableColumn column, RuleMetadata rules, DeclarationMetadata declaration,
        IReadOnlyDictionary<String, InitialMetadata>? initials, Boolean header, Func<String, String?> catalogOf)
    {
        var readOnly = IsReadOnly(column, declaration, header);
        String? catalog = null;
        Boolean? unavailable = null;
        if (column.IsRef && !column.IsSetRef && !readOnly)
        {
            catalog = column.RefTable is NormalEndpointMetadata ep ? catalogOf(ep.Path) : null;
            unavailable = catalog == null ? true : null;
        }
        return new McpField(column.Name, TypeName(column))
        {
            Required = rules.Required.Contains(column.Name) ? true : null,
            Computed = column.HasSqlAs || rules.Computed.ContainsKey(column.Name) ? true : null,
            ReadOnly = readOnly ? true : null,
            Default = Default(column, rules, initials),
            Catalog = catalog,
            Unavailable = unavailable,
            // the living values, without the filter's 'All' row (its key is the empty string)
            Values = column.IsSetRef
                ? [.. column.RefTableCheck.Storage.Values.Where(v => !v.Void && v.Id.Length > 0).Select(v => new McpSetValue(v.Id, v.Name))]
                : null
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

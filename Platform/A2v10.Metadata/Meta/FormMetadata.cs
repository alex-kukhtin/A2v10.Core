// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

namespace A2v10.Metadata;

public enum EntityCommandType
{
    Create,
    Edit,
    Delete,
    Save,
    SaveAndClose,
    Print,
    Copy,
    Show,
    Search,
    Reload,
    Post,
    UnPost,
    Attachments
}

public enum CommandBarItemKind 
{ 
    Command, 
    Separator, 
    Aligner 
}

[JsonConverter(typeof(CommandBarItemConverter))]
public readonly struct CommandBarItem
{
    public CommandBarItemKind Kind { get; }
    public EntityCommandType? Command { get; }
    private CommandBarItem(CommandBarItemKind kind, EntityCommandType? command)
    {
        Kind = kind;
        Command = command;
    }

    public static implicit operator CommandBarItem(EntityCommandType command)
        => new(CommandBarItemKind.Command, command);

    public static readonly CommandBarItem Separator = new(CommandBarItemKind.Separator, null);
    public static readonly CommandBarItem Aligner = new(CommandBarItemKind.Aligner, null);
}

public enum FormElementKind
{
    Group,
    Tabs,
    Tab,
    DataGrid,
    Taskpad,
    Toolbar,
    Pager,
    Filters
}

public enum FlowAxis
{
    Columns, // default
    Rows
}

public enum LabelAt
{
    Top, // default
    Left
}

public sealed record FormElement
{
    public FormElementKind Is { get; init; }

    /* The row set this node is anchored to, written as its parts: 'Scope' is the collection,
     * 'Kind' one of its kinds. Required together exactly when the collection declared kinds -
     * see TableMetadata.CheckKinds. 'RowSet' is the composed name they resolve to, and it is
     * the generator's, which is why the author writes neither it nor its rule.
     */
    public String? Scope { get; init; }
    public String? Kind { get; init; }

    [JsonIgnore]
    internal String? RowSet { get; init; }
    public List<FormElement> Elements { get; init; } = [];
    public List<String> Fields { get; init; } = [];
    public List<CommandBarItem> Commands { get; init; } = [];
    public FlowAxis Axis { get; init; }
    public LabelAt LabelAt { get; init; }

    [JsonIgnore]
    internal List<TableColumn> Columns { get; init; } = [];

    /* Which state property this tab strip drives - derived from the children, not declared.
     * The tabs of one strip are the kinds of one collection, so the collection is already
     * said by them; asking the author to repeat it would let the two disagree.
     */
    [JsonIgnore]
    internal String? TabState { get; init; }

    /* Resolves what the file wrote against the shape - names to columns, (scope, kind) to a row
     * set - and REBUILDS rather than fills, for the reason DeclarationMetadata.Bake rebuilds:
     * what arrives here may belong to another endpoint. An operation that declares no form of
     * its own gets the storage endpoint's by reference (MergeDeclaration), and that endpoint is
     * already published - resolving in place would be writing into its declaration.
     *
     * One walk for declared and default forms alike. The default used to be the only one that
     * ever got here, which is why a declared form reached the generator with no columns at all.
     */
    internal FormElement Bake(TableMetadata table, List<TableColumn> cols)
    {
        TableColumn FindColumn(String key) =>
           cols.FirstOrDefault(c => c.Name == key)
                ?? throw new InvalidOperationException($"field '{key}' not found in {table.SqlTableName}");

        var elements = new List<FormElement>(Elements.Count);
        String? tabState = null;
        foreach (var el in Elements)
        {
            if (String.IsNullOrEmpty(el.Scope))
            {
                elements.Add(el.Bake(table, cols));
                continue;
            }
            var detailsTable = table.FindDetails(el.Scope);
            IReadOnlyList<String> named = el.Kind == null ? [] : [el.Kind];
            detailsTable.CheckKinds(named);
            elements.Add(el.Bake(detailsTable, [.. detailsTable.AllColumns(c => c.Type != ColumnType.RowKind)])
                with { RowSet = detailsTable.RowSetName(el.Kind) });
            if (Is != FormElementKind.Tabs)
                continue;
            var state = detailsTable.TabStateName;
            if (tabState != null && tabState != state)
                throw new InvalidOperationException(
                    $"tabs mix collections ({tabState}, {state}). One strip switches one collection.");
            tabState = state;
        }
        return this with
        {
            Columns = [.. Fields.Select(FindColumn)],
            Elements = elements,
            TabState = tabState
        };
    }
}
public enum FormKind
{
    Unknown = 0,
    Page,
    Dialog
}

public sealed record FormMetadata
{    
    public FormKind Is { get; init; }
    public String? Scope { get; init; }
    public List<FormElement> Body { get; init; } = [];
    public FormElement Toolbar { get; init; } = new() { Is = FormElementKind.Toolbar };
    public FormElement Taskpad { get; init; } = new() { Is = FormElementKind.Taskpad };

    /* Every branch of the form, by one walk. The filter is the column set this form may name at
     * all - what an index shows is not what an edit lets you write - and it is applied to the
     * candidates, so a field the form may not carry reads as a field it cannot find.
     */
    internal FormMetadata Bake(TableMetadata table, Func<TableColumn, Boolean> filter)
    {
        var cols = table.AllColumns(filter).ToList();
        return this with
        {
            Body = [.. Body.Select(el => el.Bake(table, cols))],
            Toolbar = Toolbar.Bake(table, cols),
            Taskpad = Taskpad.Bake(table, cols)
        };
    }
}


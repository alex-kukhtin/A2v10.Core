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

public record FormElement
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
    internal String? RowSet { get; private set; }
    public List<FormElement> Elements { get; init; } = [];
    public List<String> Fields { get; init; } = [];
    public List<CommandBarItem> Commands { get; set; } = [];
    public FlowAxis Axis { get; init; }
    public LabelAt LabelAt { get; init; }

    [JsonIgnore]
    internal List<TableColumn> Columns { get; private set; } = [];

    /* Which state property this tab strip drives - derived from the children, not declared.
     * The tabs of one strip are the kinds of one collection, so the collection is already
     * said by them; asking the author to repeat it would let the two disagree.
     */
    [JsonIgnore]
    internal String? TabState { get; private set; }

    internal void SetDefaults(TableMetadata table, List<TableColumn> cols)
    {
        TableColumn FindColumn(String key) =>
           cols.FirstOrDefault(c => c.Name == key)
                ?? throw new InvalidOperationException($"FormMetadata. Column {key} not found");
        Columns = [.. Fields.Select(FindColumn)];
        foreach (var el in Elements)
        {
            if (!String.IsNullOrEmpty(el.Scope))
            {
                var detailsTable = table.FindDetails(el.Scope);
                IReadOnlyList<String> named = el.Kind == null ? [] : [el.Kind];
                detailsTable.CheckKinds(named);
                el.RowSet = detailsTable.RowSetName(el.Kind);
                el.SetDefaults(detailsTable, [..detailsTable.AllColumns(c => c.Type != ColumnType.RowKind)]);
                if (Is == FormElementKind.Tabs)
                {
                    var state = detailsTable.TabStateName;
                    if (TabState != null && TabState != state)
                        throw new InvalidOperationException(
                            $"FormMetadata. Tabs mix collections ({TabState}, {state}). One strip switches one collection.");
                    TabState = state;
                }
            }
            else
                el.SetDefaults(table, cols);
        }
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
    public FormElement TaskPad { get; init; } = new() { Is = FormElementKind.Taskpad };
    public FormMetadata SetDefaults(TableMetadata table, Func<TableColumn, Boolean> filter)
    {
        var cols = table.AllColumns(filter).ToList();
        foreach (var el in Body)
            el.SetDefaults(table, cols);
        TaskPad.SetDefaults(table, cols);
        return this;
    }
}


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

    /* The row set this node is anchored to, written as its parts: 'Scope' is the collection, 'Kind'
     * one of its kinds - required together exactly when the collection declared kinds, see
     * TableMetadata.CheckKinds. 'RowSet' is the composed name they resolve to, the generator's own.
     */
    public String? Scope { get; init; }
    public String? Kind { get; init; }

    [JsonIgnore]
    internal String? RowSet { get; init; }
    public List<FormElement> Elements { get; init; } = [];
    public List<String> Fields { get; init; } = [];

    // references into the endpoint's filter namespace, not fields - see CLAUDE.md, "Filters"
    public List<String> Filters { get; init; } = [];
    public List<CommandBarItem> Commands { get; init; } = [];
    public FlowAxis Axis { get; init; }
    public LabelAt LabelAt { get; init; }

    [JsonIgnore]
    internal List<MemberDescriptor> Members { get; init; } = [];

    [JsonIgnore]
    internal List<FilterDescriptor> BakedFilters { get; init; } = [];

    /* Which state property this tab strip drives - derived from the children, not declared: the
     * tabs of one strip are the kinds of one collection, so the collection is already said by them.
     */
    [JsonIgnore]
    internal String? TabState { get; init; }

    /* Resolves what the file wrote against the shape - names to members, (scope, kind) to a row
     * set. One walk for declared and default forms alike, and it rebuilds rather than fills - see
     * CLAUDE.md, "Declarations".
     */
    internal FormElement Bake(TableMetadata table, List<MemberDescriptor> members)
    {
        MemberDescriptor FindMember(String key) =>
           members.FirstOrDefault(m => m.Name == key)
                ?? throw new InvalidOperationException($"field '{key}' not found in {table.SqlTableName}");

        // candidates are the namespace, not 'cols' - so a name the form got wrong fails the load
        List<FilterDescriptor> FindFilters()
        {
            if (Filters.Count == 0)
                return [];
            var available = table.Filters().ToList();
            return [.. Filters.Select(key => available.FirstOrDefault(f => f.Name == key)
                ?? throw new InvalidOperationException(
                    $"filter '{key}' not found in {table.SqlTableName}. Available: "
                    + String.Join(", ", available.Select(f => f.Name))))];
        }

        var elements = new List<FormElement>(Elements.Count);
        String? tabState = null;
        foreach (var el in Elements)
        {
            if (String.IsNullOrEmpty(el.Scope))
            {
                elements.Add(el.Bake(table, members));
                continue;
            }
            var detailsTable = table.FindDetails(el.Scope);
            IReadOnlyList<String> named = el.Kind == null ? [] : [el.Kind];
            detailsTable.CheckKinds(named);
            elements.Add(el.Bake(detailsTable, detailsTable.RowMembers())
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
            Members = [.. Fields.Select(FindMember)],
            BakedFilters = FindFilters(),
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

    /* Every branch of the form, by one walk. 'members' is what this form may name at all - what an
     * index shows is not what an edit lets you write - so a member the form may not carry reads as
     * a member it cannot find. See CLAUDE.md, "Members".
     */
    internal FormMetadata Bake(TableMetadata table, List<MemberDescriptor> members)
    {
        return this with
        {
            Body = [.. Body.Select(el => el.Bake(table, members))],
            Toolbar = Toolbar.Bake(table, members),
            Taskpad = Taskpad.Bake(table, members)
        };
    }
}


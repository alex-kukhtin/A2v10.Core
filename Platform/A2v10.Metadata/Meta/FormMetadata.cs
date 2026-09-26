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
    ShowTrans,
    Search,
    Reload,
    Post,
    UnPost,
    Attachments
}

public enum CommandBarItemKind
{
    Command,
    Separator
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
}

public enum FormElementKind
{
    Group,
    Tabs,
    Tab,
    DataGrid,
    TreeGrid,
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

    /* Which state property this tab strip drives - the STRIP's own, not the collection's. A strip
     * is a screen and not a row set: the tabs of one may be the kinds of one collection, several
     * collections, or both at once, and "which tab is open" is a fact about the strip, which the
     * record owns, not about any collection in it - a collection does not know it is being shown,
     * let alone beside whom.
     *
     * Named after the tab it opens on ($$TabRows, $$TabStockRows), which is unique among the strips
     * of one form as long as their first tabs differ, and a strip showing one row set twice is
     * refused below. Named after the collection, it could not tell two of them apart, and that -
     * nothing in the model - is what used to forbid a strip over several collections.
     */
    [JsonIgnore]
    internal String? TabState { get; init; }

    /* Resolves what the file wrote against the shape - names to members, (scope, kind) to a row
     * set. One walk for declared and default forms alike, and it rebuilds rather than fills - see
     * CLAUDE.md, "Declarations".
     */
    /* Every refusal one node of a form can earn, in one place, run on each node while the form is
     * baked. It grows: a rule lands here the day a written form silently did nothing, and the cost
     * of each is one line against an author (or a model) writing into a key nobody reads.
     *
     * Placement first. 'scope' re-roots onto a collection, and only a tab shows one: a group lays out
     * the editors of a record, and its axis and labelAt mean nothing over rows. A tab in turn is drawn
     * only by its strip (XamlBuilder.CreateTabsScope). Refused while baking - otherwise a scoped group
     * is read against the header, and the author gets "field not found" about a field that is right
     * there in the rows.
     */
    internal static void CheckElement(FormElementKind? parent, FormElement el)
    {
        if (!String.IsNullOrEmpty(el.Scope) && el.Is != FormElementKind.Tab)
            throw new InvalidOperationException(
                $"'{Word(el.Is)}' with scope '{el.Scope}': a collection is shown only by a tab inside tabs");
        if (el.Is == FormElementKind.Tab && parent != FormElementKind.Tabs)
            throw new InvalidOperationException(
                $"tab '{el.Scope}' outside tabs: a tab is drawn only by its strip");
        /* The other half of that sentence. A strip draws its children as tabs and nothing else
         * (XamlBuilder.CreateTabsScope), so anything else inside one is a node that renders as a
         * nameless empty case; and a tab without a scope is the same thing spelled shorter - it is
         * the row set that gives a tab its content, its caption and the value the strip switches on.
         */
        if (parent == FormElementKind.Tabs && el.Is != FormElementKind.Tab)
            throw new InvalidOperationException(
                $"'{Word(el.Is)}' inside tabs: a strip shows tabs and nothing else");
        if (el.Is == FormElementKind.Tab && String.IsNullOrEmpty(el.Scope))
            throw new InvalidOperationException(
                "tab: 'scope' is not declared. A tab shows one row set, and 'scope' is how it names one");
        CheckContents(el);
    }

    // as the file spells it: the message names the key the author wrote, never the C# member
    private static String Word(FormElementKind kind) =>
        Char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

    /* What the RENDER of each kind reads - taken from XamlBuilder.ElementToControl and from nowhere
     * else, so this list is a second reading of that switch rather than a rule of its own. A kind
     * absent here is one whose content has not been described yet, and nothing is refused on it.
     *
     * Only the list-valued keys, because only they can be told apart from an absent key. 'axis' and
     * 'labelAt' are enums whose first member is the default, so 'written' and 'not written' are one
     * value - the same reason 'kinds' is examined for emptiness alone (TableMetadata.CheckKinds).
     *
     * 'tab' is absent: its content is drawn by the strip as one table of its rows, and what else it
     * may carry has not been decided. A kind is described here only from the renderer as it stands.
     */
    private static String[]? ContentKeys(FormElementKind kind) => kind switch
    {
        FormElementKind.Filters => ["filters"],
        FormElementKind.Taskpad or FormElementKind.Tabs => ["elements"],
        FormElementKind.Toolbar => ["commands"],
        FormElementKind.DataGrid or FormElementKind.TreeGrid => ["fields"],
        // its own editors, and the groups under it
        FormElementKind.Group => ["fields", "elements"],
        FormElementKind.Pager => [],
        _ => null
    };

    /* A key written on a node that never reads it is the one failure of this format that leaves no
     * trace at all: the node renders, empty, and the author writes the same thing into the next
     * place. Louder than a wrong value, because a wrong value at least draws something.
     */
    private static void CheckContents(FormElement el)
    {
        var reads = ContentKeys(el.Is);
        if (reads == null)
            return;

        IEnumerable<String> Written()
        {
            if (el.Elements.Count > 0) yield return "elements";
            if (el.Fields.Count > 0) yield return "fields";
            if (el.Filters.Count > 0) yield return "filters";
            if (el.Commands.Count > 0) yield return "commands";
        }

        var unread = Written().Where(k => !reads.Contains(k)).ToList();
        if (unread.Count == 0)
            return;
        throw new InvalidOperationException(
            $"'{Word(el.Is)}' declares {String.Join(", ", unread.Select(k => $"'{k}'"))}, which nothing reads. "
            + (reads.Length == 0
                ? "It shows no content of its own."
                : $"It shows {String.Join(", ", reads.Select(k => $"'{k}'"))} and nothing else."));
    }

    /* 'filters' is the endpoint's namespace (FilterMetadata.Filters), handed down with the members:
     * both are what this form may name, and neither is a question the shape alone can answer. A row
     * set has none - the index filters the records, never the rows inside one.
     */
    internal FormElement Bake(TableMetadata table, List<MemberDescriptor> members, IReadOnlyList<FilterDescriptor> filters)
    {
        MemberDescriptor FindMember(String key) =>
           members.FirstOrDefault(m => m.Name == key)
                ?? throw new InvalidOperationException($"field '{key}' not found in {table.SqlTableName}");

        // candidates are the namespace, not 'cols' - so a name the form got wrong fails the load
        List<FilterDescriptor> FindFilters()
        {
            if (Filters.Count == 0)
                return [];
            return [.. Filters.Select(key => filters.FirstOrDefault(f => f.Name == key)
                ?? throw new InvalidOperationException(
                    $"filter '{key}' not found in {table.SqlTableName}. Available: "
                    + String.Join(", ", filters.Select(f => f.Name))))];
        }

        var elements = new List<FormElement>(Elements.Count);
        foreach (var el in Elements)
        {
            CheckElement(Is, el);
            if (String.IsNullOrEmpty(el.Scope))
            {
                elements.Add(el.Bake(table, members, filters));
                continue;
            }
            var detailsTable = table.FindDetails(el.Scope);
            IReadOnlyList<String> named = el.Kind == null ? [] : [el.Kind];
            detailsTable.CheckKinds(named);
            elements.Add(el.Bake(detailsTable, detailsTable.RowMembers(), [])
                with { RowSet = detailsTable.RowSetName(el.Kind) });
        }
        return this with
        {
            Members = [.. Fields.Select(FindMember)],
            BakedFilters = FindFilters(),
            Elements = elements,
            TabState = Is == FormElementKind.Tabs ? StripState(elements) : null
        };
    }

    /* Asked of the BAKED tabs, because both answers are about row sets and only the bake resolves
     * (scope, kind) into one. A strip showing the same row set twice draws a second button onto a
     * case the switch can never reach - the one failure this format leaves no trace of - and an
     * empty strip is a tab bar with nothing to switch, which is what the author meant to fill.
     */
    private static String StripState(List<FormElement> tabs)
    {
        if (tabs.Count == 0)
            throw new InvalidOperationException("tabs: no tab declared. A strip switches between row sets and has none");
        var shown = new HashSet<String>();
        foreach (var tab in tabs)
            if (!shown.Add(tab.RowSet!))
                throw new InvalidOperationException(
                    $"tabs: '{tab.RowSet}' is shown by two tabs of one strip. A strip switches, so its tabs are different row sets");
        return $"$$Tab{tabs[0].RowSet}";
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
    /* The strips this form lays out, in the order it lays them out. One walk, because the template
     * declares a state property per strip and the XAML binds one per strip - two readers of a fact
     * that belongs to neither, and the list of collections is not that fact: a form may show a
     * collection without a strip, or several collections in one.
     */
    internal IEnumerable<FormElement> TabStrips()
    {
        static IEnumerable<FormElement> Walk(FormElement el)
        {
            if (el.Is == FormElementKind.Tabs)
                yield return el;
            foreach (var strip in el.Elements.SelectMany(Walk))
                yield return strip;
        }
        return Body.Append(Taskpad).SelectMany(Walk);
    }

    internal FormMetadata Bake(TableMetadata table, List<MemberDescriptor> members, IReadOnlyList<FilterDescriptor> filters)
    {
        // the two slots are nodes too: 'fields' written into a toolbar is read by nobody either
        FormElement.CheckElement(null, Toolbar);
        FormElement.CheckElement(null, Taskpad);
        return this with
        {
            Body = [.. Body.Select(el => { FormElement.CheckElement(null, el); return el.Bake(table, members, filters); })],
            Toolbar = Toolbar.Bake(table, members, filters),
            Taskpad = Taskpad.Bake(table, members, filters)
        };
    }
}


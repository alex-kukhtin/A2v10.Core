// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* No command bars here: the standard set is the template's, not the form's, and a default form
 * adds nothing to the slot. See XamlBuilder.StandardToolbar and CLAUDE.md, "Commands".
 */
internal static class DefaultFormBuilder
{
    /* No filters, no node: an empty panel still counts as content, and the taskpad would render for it.
     *
     * One control per column: the panel offers the values themselves, and a second entry over the
     * same column - the roles a state groups into - would put two questions about one field side by
     * side on every screen that never asked. The entry stays in the namespace, so a declared form
     * names it when a screen wants it; the default hides it, which is the one thing a form may do
     * to a filter (see CLAUDE.md, "Filters").
     */
    static List<FormElement> FilterElements(TableMetadata table) =>
        table.Filters().Where(f => f.Kind != FilterKind.Role).Select(f => f.Name).ToList() is { Count: > 0 } names
            ? [new FormElement() { Is = FormElementKind.Filters, Filters = names }]
            : [];

    /* The columns an index grid shows by default. A GUID key is nobody's reading - it stays in the
     * model ([Id!!Id]) and leaves the grid; a form that names Id keeps it, that is the author's call.
     */
    static IEnumerable<TableColumn> IndexGridColumns(TableMetadata table, AppPlatformId platformId) =>
        table.AllColumns(TableColumnPredicates.IsIndexColumn)
            .Where(c => !(c.Type == ColumnType.Id && platformId.ClrType == typeof(Guid)))
            .OrderBy(c => c.IsMemo);

    public static FormMetadata CreateIndexForm(TableMetadata table, AppPlatformId platformId)
    {
        if (table.Kind == EndpointKind.AccPlan)
            return CreateTreeIndexForm(table);
        var cols = IndexGridColumns(table, platformId);

        return new FormMetadata()
        {
            Is = FormKind.Page,
            Body = [
                new FormElement()
                {
                    Is = FormElementKind.DataGrid,
                    Fields = [..cols.Select(c => c.Name)]
                },
                new FormElement()
                {
                    Is = FormElementKind.Pager
                }
            ],
            Taskpad = new FormElement()
            {
                Is = FormElementKind.Taskpad,
                Elements = FilterElements(table)
            }
        };
    }

    /* A chart of accounts is read whole, as a tree: no pager, no filters. Parent is not a column
     * here - the tree itself shows it.
     */
    static FormMetadata CreateTreeIndexForm(TableMetadata table)
    {
        var cols = table.AllColumns(TableColumnPredicates.IsIndexColumn)
            .Where(c => c.Type != ColumnType.Parent);
        return new FormMetadata()
        {
            Is = FormKind.Page,
            Body = [
                new FormElement()
                {
                    Is = FormElementKind.TreeGrid,
                    Fields = [.. cols.Select(c => c.Name)]
                }
            ]
        };
    }

    public static FormMetadata CreateBrowseForm(TableMetadata table, AppPlatformId platformId)
    {
        // an account is picked from the tree the index shows
        if (table.Kind == EndpointKind.AccPlan)
            return CreateTreeIndexForm(table) with { Is = FormKind.Dialog };
        var cols = IndexGridColumns(table, platformId);

        return new FormMetadata()
        {
            Is = FormKind.Dialog,
            Body = [
                new FormElement()
                {
                    Is = FormElementKind.DataGrid,
                    Fields = [..cols.Select(c => c.Name)]
                }
            ],
            Taskpad = new FormElement()
            {
                Is = FormElementKind.Taskpad,
                Elements = FilterElements(table)
            }
        };
    }

    public static FormMetadata CreateEditForm(TableMetadata table)
    {
        return table.EditWithPage ? CreateEditPage(table) : CreateEditFormDialog(table);
    }

    /* Members an edit form carries after its columns - what a trait contributes to the record, in
     * the same 'fields' list. Last, because they are about the record rather than its fields.
     */
    static IEnumerable<String> TrailingMembers(TableMetadata table)
    {
        if (table.HasTags)
            yield return Constants.FieldNames.Tags;
    }

    static Boolean IsDetailsColumn(TableColumn col)
        => col.Type != ColumnType.Id && col.Type != ColumnType.RowKind && col.Type != ColumnType.Master;

    static Int32 SemanticDetailsOrder(TableColumn col)
        => col.Type switch
        {
            ColumnType.RowNumber => 0,
            ColumnType.Ref => 1,
            ColumnType.Float or ColumnType.Money => 3,
            _ => 4
        };


    /* One strip over every row set the record has - the kinds of a collection and the collections
     * themselves are the same kind of thing here: mutually exclusive views of what is inside this
     * record, which is what a strip says. A strip per collection instead gives a page of stacked
     * tab bars, each with one tab and nothing to switch, sharing the height between them - a
     * document with six collections reads as six sections, and the screen the user knows is gone.
     *
     * Nothing about the collection decides this any more: the state a strip drives belongs to the
     * strip (FormElement.TabState), so what is in one is a question of layout, and the default is
     * the layout that puts every row set under one switch.
     */
    static FormElement? DetailsTabs(TableMetadata table)
    {
        static IEnumerable<FormElement> CollectionTabs(String key, TableMetadata dt) =>
            dt.Kinds.Count > 0
                ? dt.Kinds.Keys.Select(k => new FormElement()
                    {
                        Is = FormElementKind.Tab,
                        Scope = key,
                        Kind = k,
                        Fields = [.. dt.AllColumns(IsDetailsColumn).OrderBy(SemanticDetailsOrder).Select(c => c.Name)]
                    })
                : [new FormElement()
                    {
                        Is = FormElementKind.Tab,
                        Scope = key,
                        Fields = [.. dt.AllColumns(IsDetailsColumn).Select(c => c.Name)]
                    }];

        List<FormElement> tabs = [.. table.Details.SelectMany(d => CollectionTabs(d.Key, d.Value))];
        return tabs.Count == 0
            ? null
            : new FormElement() { Is = FormElementKind.Tabs, Elements = tabs };
    }

    public static FormMetadata CreateEditPage(TableMetadata table)
    {
        static Int32 GroupNumber(TableColumn c) => c.Type switch {
            ColumnType.Operation => 1,
            ColumnType.Autonum => 1,
            ColumnType.Date => 1,
            ColumnType.Memo => 3,
            _ => 2
        };
        var cols = table.AllColumns(TableColumnPredicates.IsEditColumn).ToList();

        var topCols = cols.Where(c => GroupNumber(c) == 1).OrderBy(c => !c.IsOperation);
        var middleCols = cols.Where(c => GroupNumber(c) == 2);
        var bottomCols = cols.Where(c => GroupNumber(c) == 3);

        var fd = new FormMetadata()
        {
            Is = FormKind.Page,
            Body = [
                new FormElement()
                {
                    Is = FormElementKind.Group,
                    LabelAt = LabelAt.Left,
                    Axis = FlowAxis.Rows,
                    Fields = [..topCols.Select(c => c.Name)]
                },
                new FormElement()
                {
                    Is = FormElementKind.Group,
                    Axis = FlowAxis.Rows,
                    Fields = [..middleCols.Select(c => c.Name)]
                }
            ]
        };

        if (DetailsTabs(table) is FormElement tabs)
            fd.Body.Add(tabs);

        fd.Body.Add(new FormElement()
        {
            Is = FormElementKind.Group,
            Axis = FlowAxis.Rows,
            LabelAt = LabelAt.Left,
            Fields = [.. bottomCols.Select(c => c.Name), .. TrailingMembers(table)]
        });

        return fd;
    }

    public static FormMetadata CreateEditFormDialog(TableMetadata table)
    {
        // TODO!!!
        var cols = table.AllColumns(TableColumnPredicates.IsEditColumn)
            .OrderBy(c => c.IsMemo);
            //.ToDictionary(c => c.Name, c => new FormColumn());

        var fd = new FormMetadata()
        {
            Is = FormKind.Dialog,
            Body = [
                new FormElement()
                {
                    Is = FormElementKind.Group,
                    Fields = [..cols.Select(c => c.Name), .. TrailingMembers(table)]
                }
            ]
        };

        if (DetailsTabs(table) is FormElement tabs)
            fd.Body.Add(tabs);
        return fd;
    }
}

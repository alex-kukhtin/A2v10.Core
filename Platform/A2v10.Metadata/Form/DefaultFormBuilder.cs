// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

internal static class DefaultFormBuilder
{
    /* Print exists when the endpoint declares a blank to print, and it carries its own leading
     * separator so that it leaves without doubling the next one - the posting group below is the
     * same shape. ONE command on both toolbars: printing is one act, and that the card hands it the
     * record while the grid hands it the selected row is a property of the screen, not a second
     * command - see CommandScope. A screen may hide the button, but whether the entity prints at
     * all is not a per-screen answer. See CLAUDE.md, "Commands".
     */
    static List<CommandBarItem> PrintCommand(DeclarationMetadata declaration) =>
        declaration.PrintForms.Count > 0
            ? [CommandBarItem.Separator, EntityCommandType.Print]
            : [];

    /* The declaration for the same reason the edit form takes one: whether this endpoint prints at
     * all is answered by its blanks, not by the shape. See CLAUDE.md, "Commands".
     */
    public static FormMetadata CreateIndexForm(TableMetadata table, DeclarationMetadata declaration)
    {
        var cols = table.AllColumns(TableColumnPredicates.IsIndexColumn)
            .OrderBy(c => c.IsMemo);

        List<CommandBarItem> indexCommands() =>
            table.Kind switch
            {
                EndpointKind.Catalog =>
                    [
                        EntityCommandType.Create, EntityCommandType.Edit, EntityCommandType.Delete,
                        CommandBarItem.Separator, EntityCommandType.Show, CommandBarItem.Separator, EntityCommandType.Reload,
                        CommandBarItem.Aligner, EntityCommandType.Search
                    ],
                EndpointKind.Document =>
                    [
                        EntityCommandType.Create, EntityCommandType.Edit, EntityCommandType.Delete,
                        .. PrintCommand(declaration),
                        CommandBarItem.Separator, EntityCommandType.Reload,
                        CommandBarItem.Aligner, EntityCommandType.Search
                    ],
                EndpointKind.Journal =>
                    [
                        EntityCommandType.Edit,
                        CommandBarItem.Separator, EntityCommandType.Reload,
                        CommandBarItem.Aligner, EntityCommandType.Search
                    ],
                EndpointKind.Operation => [],
                _ => throw new InvalidOperationException($"Unsupported comamnds for {table.Schema}")
            };

        return new FormMetadata()
        {
            Is = FormKind.Page,
            Body = [
                new FormElement() {
                    Is = FormElementKind.Toolbar,
                    Commands = indexCommands()
                },
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
                Elements = [
                    new FormElement() {
                        Is = FormElementKind.Filters,
                        Filters = [..table.Filters().Select(f => f.Name)],
                    }
                ]
            }
        };
    }

    public static FormMetadata CreateBrowseForm(TableMetadata table)
    {
        var cols = table.AllColumns(TableColumnPredicates.IsIndexColumn)
            .OrderBy(c => c.IsMemo);
            //.ToDictionary(c => c.Name, c => new FormColumn());

        return new FormMetadata()
        {
            Is = FormKind.Dialog,
            Body = [
                new FormElement() {
                    Is = FormElementKind.Toolbar,
                    Commands = [
                        EntityCommandType.Create, EntityCommandType.Edit, EntityCommandType.Delete,
                        CommandBarItem.Separator, EntityCommandType.Reload,
                        CommandBarItem.Aligner, EntityCommandType.Search
                    ]
                },
                new FormElement()
                {
                    Is = FormElementKind.DataGrid,
                    Fields = [..cols.Select(c => c.Name)]
                }
            ],
            Taskpad = new FormElement()
            {
                Is = FormElementKind.Taskpad,
                Elements = [
                    new FormElement() {
                        Is = FormElementKind.Filters,
                        Filters = [..table.Filters().Select(f => f.Name)],
                    }
                ]
            }
        };
    }

    /* The declaration and not only the shape: which commands EXIST for this entity is the
     * endpoint's question, not the table's - a document whose endpoint declares no 'post' has no
     * Post, no UnPost and nothing to show in the transactions dialog. See CLAUDE.md, "Commands".
     */
    public static FormMetadata CreateEditForm(TableMetadata table, DeclarationMetadata declaration)
    {
        return table.EditWithPage ? CreateEditPage(table, declaration) : CreateEditFormDialog(table);
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
        => col.Type != ColumnType.Id && col.Type != ColumnType.RowKind && col.Type != ColumnType.Owner;

    static Int32 SemanticDetailsOrder(TableColumn col)
        => col.Type switch
        {
            ColumnType.RowNumber => 0,
            ColumnType.Ref => 1,
            ColumnType.Float or ColumnType.Money => 3,
            _ => 4
        };


    /* One strip per collection, holding that collection's kinds - because the state a strip
     * drives is per collection too. Flattening every collection into a single strip would put
     * the kinds of 'Rows' and of 'Links' on one switch with one value.
     */
    static IEnumerable<FormElement> DetailsTabs(TableMetadata table)
    {
        foreach (var d in table.Details)
        {
            var dt = d.Value;
            List<FormElement> tabs = dt.Kinds.Count > 0
                ? [.. dt.Kinds.Keys.Select(k => new FormElement()
                    {
                        Is = FormElementKind.Tab,
                        Scope = d.Key,
                        Kind = k,
                        Fields = [.. dt.AllColumns(IsDetailsColumn).OrderBy(SemanticDetailsOrder).Select(c => c.Name)]
                    })]
                : [new FormElement()
                    {
                        Is = FormElementKind.Tab,
                        Scope = d.Key,
                        Fields = [.. dt.AllColumns(IsDetailsColumn).Select(c => c.Name)]
                    }];
            yield return new FormElement()
            {
                Is = FormElementKind.Tabs,
                Elements = tabs
            };
        }
    }

    public static FormMetadata CreateEditPage(TableMetadata table, DeclarationMetadata declaration)
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

        IEnumerable<CommandBarItem> Commands()
        {
            yield return EntityCommandType.SaveAndClose;
            yield return EntityCommandType.Save;
            foreach (var p in PrintCommand(declaration))
                yield return p;
            /* The whole posting group or none of it - including its leading separator, which
             * otherwise doubles up with the next one. Removing the button is not the same act as
             * the entity not having the command: this is the second.
             */
            if (declaration.Post is { Count: > 0 })
            {
                yield return CommandBarItem.Separator;
                yield return EntityCommandType.Post;
                yield return EntityCommandType.UnPost;
                yield return EntityCommandType.ShowTrans;
            }
            yield return CommandBarItem.Separator;
            if (table.Traits.Contains(TableTrait.Attachments))
            {
                yield return EntityCommandType.Attachments;
                yield return CommandBarItem.Separator;
            }
            yield return EntityCommandType.Reload;
        }

        var fd = new FormMetadata()
        {
            Is = FormKind.Page,
            Toolbar = new FormElement
            {
                Is = FormElementKind.Toolbar,
                Commands = [..Commands()]
            },
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

        foreach (var tabs in DetailsTabs(table))
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

        foreach (var tabs in DetailsTabs(table))
            fd.Body.Add(tabs);
        return fd;
    }
}

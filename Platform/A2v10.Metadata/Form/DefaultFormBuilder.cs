// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

internal static class DefaultFormBuilder
{
    public static FormMetadata CreateIndexForm(TableMetadata table)
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
                        // TODO: print command for traits = Print
                        EntityCommandType.Create, EntityCommandType.Edit, EntityCommandType.Delete,
                        CommandBarItem.Separator, EntityCommandType.Print, CommandBarItem.Separator, EntityCommandType.Reload,
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

        IEnumerable<CommandBarItem> Commands()
        {
            yield return EntityCommandType.SaveAndClose; 
            yield return EntityCommandType.Save;
            if (table.Traits.Contains(TableTrait.Print))
                yield return EntityCommandType.Print;
            yield return CommandBarItem.Separator;
            yield return EntityCommandType.Post;
            yield return EntityCommandType.UnPost;
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

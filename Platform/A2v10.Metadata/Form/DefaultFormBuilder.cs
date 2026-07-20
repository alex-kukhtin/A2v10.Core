// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Office2016.Excel;

namespace A2v10.Metadata;

internal static class DefaultFormBuilder
{
    public static FormMetadata CreateIndexForm(TableMetadata table)
    {
        var cols = table.AllColumns(TableColumnPredicates.IsIndexColumn)
            .OrderBy(c => c.IsMemo);
            //.ToDictionary(c => c.Name, c => new FormColumn());

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
            TaskPad = new FormElement()
            {
                Is = FormElementKind.Taskpad,
                Elements = [
                    new FormElement() {
                        Is = FormElementKind.Filters,
                        Fields = [..table.TableFilters().Select(f => f.Name)],
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
            TaskPad = new FormElement()
            {
                Is = FormElementKind.Taskpad,
                Elements = [
                    new FormElement() {
                        Is = FormElementKind.Filters,
                        Fields = [..table.TableFilters().Select(f => f.Name)],
                    }
                ]
            }
        };
    }

    public static FormMetadata CreateEditForm(TableMetadata table)
    {
        return table.EditWithPage ? CreateEditPage(table) : CreateEditFormDialog(table);
    }

    static Boolean IsDetailsColumn(TableColumn col)
        => col.Type != ColumnType.Id && col.Type != ColumnType.RowKind && col.Type != ColumnType.Owner;

    static IEnumerable<FormElement> DetailsTab(TableMetadata table)
    {
        foreach (var d in table.Details)
        {
            if (d.Value.Kinds.Count > 0)
            {
                foreach (var k in d.Value.Kinds)
                {
                    yield return new FormElement()
                    {
                        Is = FormElementKind.Tab,
                        Scope = k,
                        Fields = [.. d.Value.AllColumns(IsDetailsColumn).Select(c => c.Name)]
                    };
                }
            }
            else
            {
                yield return new FormElement()
                {
                    Is = FormElementKind.Tab,
                    Scope = d.Key,
                    Fields = [.. d.Value.AllColumns(IsDetailsColumn).Select(c => c.Name)]
                };
            }
        }
    }

    public static FormMetadata CreateEditPage(TableMetadata table)
    {
        Int32 GroupNumber(TableColumn c) => c.Type switch {
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
            Toolbar = new FormElement
            {
                Is = FormElementKind.Toolbar,
                Commands =
                    [
                    EntityCommandType.SaveAndClose, EntityCommandType.Save,
                    EntityCommandType.Print, CommandBarItem.Separator,
                    EntityCommandType.Post, EntityCommandType.UnPost, CommandBarItem.Separator, EntityCommandType.Attachments,
                    CommandBarItem.Separator, EntityCommandType.Reload
                ],
            },
            Body = [
                new FormElement() 
                {
                    Is = FormElementKind.Group,
                    Fields = [..topCols.Select(c => c.Name)]
                },
                new FormElement()
                {
                    Is = FormElementKind.Group,
                    Fields = [..middleCols.Select(c => c.Name)]
                }
            ]
        };

        if (table.Details.Count > 0) 
        {
            var tabs = new FormElement()
            {
                Is = FormElementKind.Tabs,
                Elements = [.. DetailsTab(table)]
            };
            fd.Body.Add(tabs);
        }

        fd.Body.Add(new FormElement()
        {
            Is = FormElementKind.Group,
            Fields = [.. bottomCols.Select(c => c.Name)]
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
                    Fields = [..cols.Select(c => c.Name)]
                }
            ]
        };

        if (table.Details.Count > 0)
        {
            var tabs = new FormElement()
            {
                Is = FormElementKind.Tabs,
                Elements = [..DetailsTab(table)]
            };
            fd.Body.Add(tabs);
        }
        return fd;
    }
}

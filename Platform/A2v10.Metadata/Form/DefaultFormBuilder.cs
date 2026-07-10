// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Office2013.Drawing.Chart;
using DocumentFormat.OpenXml.Wordprocessing;

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

    public static FormMetadata CreateEditPage(TableMetadata table)
    {
        var cols = table.AllColumns(TableColumnPredicates.IsEditColumn)
            .OrderBy(c => c.IsMemo);
            //.ToDictionary(c => c.Name, c => new FormColumn());

        // TODO: разбить cols на ТРИ части. (Date,No), (Refs), (Memo)

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
                    Fields = [..cols.Select(c => c.Name)]
                }
            ]
        };

        if (table.Details.Count > 0) 
        {
            var tabs = new FormElement()
            {
                Is = FormElementKind.Tabs
            };
            fd.Body.Add(tabs);
            foreach (var d in table.Details)
            {
                if (d.Value.Kinds.Count > 0)
                {
                    foreach (var k in d.Value.Kinds)
                    {
                        tabs.Elements.Add(new FormElement()
                        {
                            Is = FormElementKind.Tab,
                            Scope = k,
                            Fields = [..d.Value.Columns.Where(c => c.Name != "Kind").Select(c => c.Name)]
                        });
                    }
                }
            }
        }

        return fd;
    }

    public static FormMetadata CreateEditFormDialog(TableMetadata table)
    {
        // TODO!!!
        var cols = table.AllColumns(TableColumnPredicates.IsEditColumn)
            .OrderBy(c => c.IsMemo);
            //.ToDictionary(c => c.Name, c => new FormColumn());

        return new FormMetadata()
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
    }
}

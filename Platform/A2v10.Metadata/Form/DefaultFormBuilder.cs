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
            .OrderBy(c => c.IsMemo)
            .ToDictionary(c => c.Name, c => new FormColumn());

        List<CommandBarItem> indexCommands() =>
            table.Kind switch
            {
                EndpointKind.Catalog =>
                    [
                        EntityCommandType.Add, EntityCommandType.Edit, EntityCommandType.Delete,
                        CommandBarItem.Separator, EntityCommandType.Show, CommandBarItem.Separator, EntityCommandType.Reload,
                        CommandBarItem.Aligner, EntityCommandType.Search
                    ],
                EndpointKind.Document =>
                    [
                        EntityCommandType.Add, EntityCommandType.Edit, EntityCommandType.Delete,
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
            Elements = [
                new FormToolbar() {
                    Commands = indexCommands()
                },
                new FormDataGrid() 
                {
                    Columns = cols
                },
                new FormPager() 
                {
                }
            ],
            TaskPad = new FormTaskPad()
            {
                Filters = [.. table.TableFilters()]
            }
        };
    }

    public static FormMetadata CreateBrowseForm(TableMetadata table)
    {
        var cols = table.AllColumns(TableColumnPredicates.IsIndexColumn)
            .OrderBy(c => c.IsMemo)
            .ToDictionary(c => c.Name, c => new FormColumn());

        return new FormMetadata()
        {
            Is = FormKind.Dialog,
            Elements = [
                new FormToolbar() {
                    Commands = [
                        EntityCommandType.Add, EntityCommandType.Edit, EntityCommandType.Delete,
                        CommandBarItem.Separator, EntityCommandType.Reload,
                        CommandBarItem.Aligner, EntityCommandType.Search
                    ]
                },
                new FormDataGrid()
                {
                    Columns = cols
                }
            ],
            TaskPad = new FormTaskPad()
            {
                Filters = [.. table.TableFilters()]
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
            .OrderBy(c => c.IsMemo)
            .ToDictionary(c => c.Name, c => new FormColumn());

        return new FormMetadata()
        {
            Is = FormKind.Page,
            Toolbar = [
                EntityCommandType.SaveAndClose, EntityCommandType.Save,
                EntityCommandType.Print, CommandBarItem.Separator,
                EntityCommandType.Post, CommandBarItem.Separator, EntityCommandType.Attachments,
                CommandBarItem.Separator, EntityCommandType.Reload
            ],
            Elements = [
                new FormGrid() 
                {
                    Columns = cols
                }
            ]
        };
    }

    public static FormMetadata CreateEditFormDialog(TableMetadata table)
    {
        // TODO!!!
        var cols = table.AllColumns(TableColumnPredicates.IsEditColumn)
            .OrderBy(c => c.IsMemo)
            .ToDictionary(c => c.Name, c => new FormColumn());

        return new FormMetadata()
        {
            Is = FormKind.Dialog,
            Elements = [
                new FormGrid()
                {
                    Columns = cols
                }
            ]
        };
    }
}

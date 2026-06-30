// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System.Linq;

namespace A2v10.Metadata;

internal static class DefaultFormBuilder
{
    public static FormMetadata CreateIndexForm(TableMetadata table)
    {
        var cols = table.AllColumns(TableColumnPredicates.IsIndexColumn)
            .OrderBy(c => c.IsMemo)
            .ToDictionary(c => c.Name, c => new FormColumn());

        return new FormPage()
        {
            Elements = [
                new FormToolbar() {
                    Commands = [
                        EntityCommandType.Add, EntityCommandType.Edit, EntityCommandType.Delete,
                        CommandBarItem.Separator, EntityCommandType.Show, CommandBarItem.Separator, EntityCommandType.Reload,
                        CommandBarItem.Aligner, EntityCommandType.Search
                    ]
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

        return new FormDialog()
        {
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
            ]
        };
    }

    public static FormMetadata CreateEditForm(TableMetadata table)
    {
        var cols = table.AllColumns(TableColumnPredicates.IsEditColumn)
            .OrderBy(c => c.IsMemo)
            .ToDictionary(c => c.Name, c => new FormColumn());

        return new FormDialog()
        {
            Toolbar = [
                EntityCommandType.Save, EntityCommandType.SaveAndClose,
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
}

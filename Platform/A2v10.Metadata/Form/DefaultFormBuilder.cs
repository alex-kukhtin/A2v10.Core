// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

internal static class DefaultFormBuilder
{
    public static FormMetadata CreateIndexForm(TableMetadata table)
    {
        static Boolean includeColumn(TableColumn col)
            => col.Type != ColumnType.RowVersion && col.Type != ColumnType.Void;

        var cols = new Dictionary<String, FormColumn>();
        // memo => last
        foreach (var column in table.AllColumns(c => !c.IsMemo && includeColumn(c)))
            cols.Add(column.Name, new());

        var memo = table.DefaultColumns().FirstOrDefault(c => c.IsMemo);
        if (memo != null)
            cols.Add(memo.Name, new());

        var filters = table.AllColumns(c => c.IsRef).Select(c => c.Name);

        return new FormMetadata()
        {
            Columns = cols,
            Commands = [
                FormCommandType.Add, FormCommandType.Edit, FormCommandType.Delete,
                FormCommandType.Sep, FormCommandType.Show, FormCommandType.Sep, FormCommandType.Reload,
                FormCommandType.ToRight, FormCommandType.Search
            ],
            Filters = filters.ToList()
        };
    }

    public static FormMetadata CreateEditForm(TableMetadata table)
    {
        var cols = new Dictionary<String, FormColumn>();

        Boolean IsEditableColumn(TableColumn col) =>
            col.Type != ColumnType.Void && col.Type != ColumnType.RowVersion && col.Type != ColumnType.Id &&
            col.Type != ColumnType.IsSystem;

        // memo => last
        foreach (var column in table.AllColumns().Where(c => IsEditableColumn(c) && !c.IsMemo))
            cols.Add(column.Name, new());
        var memo = table.DefaultColumns().FirstOrDefault(c => c.IsMemo);
        if (memo != null)
            cols.Add(memo.Name, new());

        return new FormMetadata()
        {
            Columns = cols,
            Commands = [
                FormCommandType.Save, FormCommandType.SaveAndClose,
                FormCommandType.Print, FormCommandType.Sep,
                FormCommandType.Apply, FormCommandType.Sep, FormCommandType.Attachments,
                FormCommandType.Sep, FormCommandType.Reload
            ]
        };
    }
}

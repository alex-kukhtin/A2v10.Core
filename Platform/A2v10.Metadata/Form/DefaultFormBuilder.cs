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
        foreach (var column in table.AllColumns().Where(c => !c.IsMemo && includeColumn(c)))
            cols.Add(column.Name, new());

        var memo = table.DefaultColumns().FirstOrDefault(c => c.IsMemo);
        if (memo != null)
            cols.Add(memo.Name, new());

        return new FormMetadata()
        {
            Columns = cols
        };
    }

    public static FormMetadata CreateEditForm(TableMetadata table)
    {
        var cols = new Dictionary<String, FormColumn>();
        // memo => last
        foreach (var column in table.DefaultColumns().Where(c => !c.IsMemo))
            cols.Add(column.Name, new());
        foreach (var column in table.Columns)
            cols.Add(column.Name, new FormColumn());
        foreach (var column in table.DefaultColumns().Where(c => c.IsMemo))
            cols.Add(column.Name, new());

        return new FormMetadata()
        {
            Columns = cols
        };
    }
}

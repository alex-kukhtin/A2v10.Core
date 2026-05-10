// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

internal static class DefaultFormBuilder
{
    public static FormMetadata CreateIndexForm(TableMetadata table)
    {
        var cols = new Dictionary<String, FormColumn>()
        {
            [Constants.FieldNames.Id] = new()
        };
        // memo => last
        foreach (var column in table.DefaultColumns().Where(c => !c.IsMemo))
            cols.Add(column.Name, new());

        foreach (var column in table.Columns)
            cols.Add(column.Name, new());

        foreach (var column in table.DefaultColumns().Where(c => c.IsMemo))
            cols.Add(column.Name, new());

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

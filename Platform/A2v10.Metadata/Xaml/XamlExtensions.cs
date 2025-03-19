// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using A2v10.Xaml;

namespace A2v10.Metadata;

internal static class XamlExtensions
{
    public static ColumnRole ToColumnRole(this ColumnDataType dataType, Boolean isReference)
    {
        return dataType switch
        {
            ColumnDataType.BigInt => isReference ? ColumnRole.Default : ColumnRole.Id,
            ColumnDataType.Bit => ColumnRole.CheckBox,
            ColumnDataType.Money or ColumnDataType.Float or ColumnDataType.Int
                => ColumnRole.Number,
            ColumnDataType.Date or ColumnDataType.DateTime => ColumnRole.Date,
            _ => ColumnRole.Default,
        };
    }
    public static IEnumerable<DataGridColumn> IndexColumns(this Form form)
    {
        return form.Columns.Select(c =>
            new DataGridColumn()
            {
                Header = c.Header ?? $"@[{c.Path}]",
                Role = c.Role,
                LineClamp = c.Clamp,
                SortProperty = c.SortProperty,
                Bindings = b => b.SetBinding(nameof(DataGridColumn.Content), new Bind(c.Path))
            }
        );
    }
}

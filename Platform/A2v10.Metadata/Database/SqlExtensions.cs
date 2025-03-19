// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class SqlExtensions
{
    public static String SqlDataType(this TableColumn column, ColumnDataType idDataType)
    {
        var idDataStr = idDataType.ToString().ToLowerInvariant(); 
        return column.DataType switch
        {
            ColumnDataType.Id => idDataStr,
            ColumnDataType.Reference => idDataStr,
            ColumnDataType.String => $"nvarchar({column.MaxLength})",
            ColumnDataType.NVarChar => $"nvarchar({column.MaxLength})",
            ColumnDataType.NChar => $"nchar({column.MaxLength})",
            _ => column.DataType.ToString().ToLowerInvariant(),
        };
    }
}

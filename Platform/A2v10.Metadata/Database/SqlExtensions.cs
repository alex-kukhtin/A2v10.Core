// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

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

    internal static IEnumerable<(TableColumn Column, Int32 Index)> RefFields(this TableMetadata table)
    {
        var index = 0;
        return table.Columns.Where(c => c.IsReference).Select(c => (Column: c, Index: ++index)).ToList();
    }

    internal static IEnumerable<String> AllSqlFields(this TableMetadata table, String alias, AppMetadata appMeta)
    {
        var refFields = table.RefFields();
        foreach (var c in table.Columns.Where(c => !c.IsReference))
            if (c.Name == appMeta.IdField)
                yield return $"[{c.Name}!!Id] = {alias}.[{c.Name}]";
            else
                yield return c.IsParent ? $"ParentElem = {alias}.[{c.Name}]" : $"{alias}.[{c.Name}]";
        foreach (var c in refFields)
        {
            var col = c.Column;
            var colRef = c.Column.Reference;
            var sf = String.Empty;
            if (col.IsParent)
                sf = "Elem";
            yield return $"[{col.Name}{sf}.{appMeta.IdField}!{colRef.ModelType}!Id] = r{c.Index}.[{appMeta.IdField}], [{col.Name}{sf}.Name!{colRef.ModelType}!Name] = r{c.Index}.[{appMeta.NameField}]";
        }
    }
}

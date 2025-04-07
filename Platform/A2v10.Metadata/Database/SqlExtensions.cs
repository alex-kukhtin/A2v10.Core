// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace A2v10.Metadata;

internal static class SqlExtensions
{
    public static SqlDbType ToSqlDbType(this ColumnDataType columnDataType, ColumnDataType idDataType)
    {
        return columnDataType switch
        {
            ColumnDataType.Id or ColumnDataType.Reference => idDataType.ToSqlDbType(idDataType),
            ColumnDataType.Operation => SqlDbType.NVarChar,   
            ColumnDataType.BigInt => SqlDbType.BigInt,
            ColumnDataType.Int => SqlDbType.Int,
            ColumnDataType.SmallInt => SqlDbType.SmallInt,
            ColumnDataType.String => SqlDbType.NVarChar,
            ColumnDataType.DateTime => SqlDbType.DateTime,
            ColumnDataType.Date => SqlDbType.Date,
            ColumnDataType.Money => SqlDbType.Money,
            ColumnDataType.Float => SqlDbType.Float,
            ColumnDataType.Stream or ColumnDataType.VarBinary => SqlDbType.VarBinary,
            ColumnDataType.Uniqueidentifier => SqlDbType.UniqueIdentifier,
            _ => throw new NotSupportedException($"{columnDataType} is not supported")
        };
    }

    public static String SqlDataType(this TableColumn column, ColumnDataType idDataType)
    {
        var idDataStr = idDataType.ToString().ToLowerInvariant(); 
        var maxLength = column.MaxLength == 0 ? "max" : column.MaxLength.ToString();
        return column.DataType switch
        {
            ColumnDataType.Id => idDataStr,
            ColumnDataType.Reference or ColumnDataType.Enum => idDataStr,
            ColumnDataType.Operation => "nvarchar(64)",
            ColumnDataType.String => $"nvarchar({maxLength})",
            ColumnDataType.NVarChar => $"nvarchar({maxLength})",
            ColumnDataType.NChar => $"nchar({maxLength})",
            ColumnDataType.Stream => $"varbinary(max)",
            ColumnDataType.Uniqueidentifier => "uniqueidentifier",
            _ => column.DataType.ToString().ToLowerInvariant(),
        };
    }
    public static Type ClrDataType(this TableColumn column, ColumnDataType idDataType)
    {
        Type idType = idDataType switch
        {
            ColumnDataType.BigInt => typeof(Int64),
            ColumnDataType.Uniqueidentifier => typeof(Guid),
            _ => throw new InvalidOperationException($"Invalid Id Data Type: {idDataType}")
        };
        return column.DataType switch
        {
            ColumnDataType.Id or ColumnDataType.Reference 
                or ColumnDataType.Enum => idType,
            ColumnDataType.Operation => typeof(String),                
            ColumnDataType.BigInt => typeof(Int64),
            ColumnDataType.String or ColumnDataType.NVarChar or
                ColumnDataType.NChar => typeof(String),
            ColumnDataType.Date or ColumnDataType.DateTime => typeof(DateTime),
            ColumnDataType.Bit => typeof(Boolean),
            ColumnDataType.Money => typeof(Decimal),
            ColumnDataType.Float => typeof(Double),
            ColumnDataType.Int => typeof(Int32),
            ColumnDataType.SmallInt => typeof(Int16),
            ColumnDataType.Stream => typeof(Byte[]),
            ColumnDataType.Uniqueidentifier => typeof(Guid),
            _ => throw new InvalidOperationException($"Invalid Data Type for update. ({column.DataType})"),
        };
    }

    internal static Boolean IsFieldUpdated(this TableColumn column)
    {
        return column.Role != TableColumnRole.PrimaryKey
            && column.Role != TableColumnRole.Void
            && column.Role != TableColumnRole.IsFolder
            && column.Role != TableColumnRole.IsSystem
            && column.Name != "Owner"
            && column.Name != "Folder"
            && column.Role != TableColumnRole.Done;
    }

    internal static IEnumerable<(TableColumn Column, Int32 Index)> RefFields(this TableMetadata table)
    {
        var index = 0;
        return table.Columns.Where(c => c.IsReference).Select(c => (Column: c, Index: ++index)).ToList();
    }

    internal static IEnumerable<String> AllSqlFields(this TableMetadata table, String alias, AppMetadata appMeta, Boolean isDetails = false)
    {
        var refFields = table.RefFields();
        foreach (var c in table.Columns.Where(c => !c.IsReference))
            if (c.Role == TableColumnRole.PrimaryKey)
                yield return $"[{c.Name}!!Id] = {alias}.[{c.Name}]";
            else if (c.Role == TableColumnRole.RowNo)
                yield return $"[{c.Name}!!RowNumber] = {alias}.[{c.Name}]";
            else
                yield return c.IsParent ? $"Folder = {alias}.[{c.Name}]" : $"{alias}.[{c.Name}]";
        foreach (var c in refFields)
        {
            if (isDetails && c.Column.Role == TableColumnRole.Parent)
                continue;
            var col = c.Column;
            var colRef = c.Column.Reference;
            var elemName = col.Name;
            // TR, not T - avoid recursion 
            var modelType = $"TR{col.Reference.RefTable.Singular()}";
            if (col.IsParent)
            {
                elemName = "Folder";
                modelType = "TFolder";
            }
            yield return $"[{elemName}.{appMeta.IdField}!{modelType}!Id] = r{c.Index}.[{appMeta.IdField}], [{elemName}.Name!{modelType}!Name] = r{c.Index}.[{appMeta.NameField}]";
        }
    }
}

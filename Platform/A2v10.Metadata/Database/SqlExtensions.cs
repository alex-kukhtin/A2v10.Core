// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace A2v10.Metadata;

internal static class SqlExtensions
{
    public static String LocalizeSql(this String value)
    {
        if (String.IsNullOrEmpty(value))
            return String.Empty;
        value = value.Replace("'", "''");
        if (value.StartsWith('@'))
            return $"@[{value[1..]}]";
        return value;
    }
    public static SqlDbType ToSqlDbType(this ColumnType columnDataType, ColumnType idDataType)
    {
        return columnDataType switch
        {
            ColumnType.Id or ColumnType.Ref => idDataType.ToSqlDbType(idDataType),
            ColumnType.Operation => SqlDbType.NVarChar,
            ColumnType.Enum => SqlDbType.NVarChar,
            ColumnType.BigInt => SqlDbType.BigInt,
            ColumnType.Int => SqlDbType.Int,
            ColumnType.SmallInt => SqlDbType.SmallInt,
            ColumnType.Decimal => SqlDbType.Decimal,
            ColumnType.String => SqlDbType.NVarChar,
            ColumnType.DateTime => SqlDbType.DateTime,
            ColumnType.Date => SqlDbType.Date,
            ColumnType.Money => SqlDbType.Money,
            ColumnType.Float => SqlDbType.Float,
            ColumnType.Stream or ColumnType.VarBinary => SqlDbType.VarBinary,
            ColumnType.Uniqueidentifier => SqlDbType.UniqueIdentifier,
            _ => throw new NotSupportedException($"{columnDataType} is not supported")
        };
    }

    public static String ToSqlDataType(this ColumnType columnDataType, ColumnType idDataType, String maxLength = "255", Int32 scale = 0, Boolean toTableType = false)
    {
        var idDataStr = idDataType.ToString().ToLowerInvariant();
        return columnDataType switch
        {
            ColumnType.Id => idDataStr,
            ColumnType.Ref => idDataStr,
            ColumnType.Operation => "nvarchar(64)",
            ColumnType.Enum => "nvarchar(16)",
            ColumnType.String => $"nvarchar({maxLength})",
            ColumnType.NVarChar => $"nvarchar({maxLength})",
            ColumnType.NChar => $"nchar({maxLength})",
            ColumnType.Stream => $"varbinary(max)",
            ColumnType.Uniqueidentifier => "uniqueidentifier",
            ColumnType.RowVersion => toTableType ? "varbinary(8)" : "rowversion",
            ColumnType.Decimal => $"decimal({maxLength},{scale})",
            _ => columnDataType.ToString().ToLowerInvariant(),
        };
    }

    public static String SqlDataType(this TableColumn column, ColumnType idDataType, Boolean toTableType = false)
    {
        var maxLength = column.MaxLength == 0 ? "max" : column.MaxLength.ToString();
        return column.Type.ToSqlDataType(idDataType, maxLength, column.Scale, toTableType);
    }

    public static String SqlModelColumnName(this TableColumn column, String alias, ReferenceMember? refMember)
    {
        if (column.Role == TableColumnRole.Name)
            return $"[Name!!Name] = {alias}.[Name]";
        else if (column.Type == ColumnType.Ref) {
            refMember = refMember ?? throw new InvalidOperationException($"ReferenceMember is required for Ref column {column.Name}");
            return $"[{column.Name}!{refMember.Table.TypeName}!RefId] = {alias}.[{column.Name}]";
        }
        return $"{alias}.[{column.Name}]";
    }

    public static Type ClrDataType(this TableColumn column)
    {
        return column.Type switch
        {
            ColumnType.Id or ColumnType.Ref => typeof(Int64),
            ColumnType.Operation => typeof(String),
            ColumnType.Enum => typeof(String),
            ColumnType.BigInt => typeof(Int64),
            ColumnType.String or ColumnType.NVarChar or
                ColumnType.NChar => typeof(String),
            ColumnType.Date or ColumnType.DateTime => typeof(DateTime),
            ColumnType.Bit => typeof(Boolean),
            ColumnType.Money => typeof(Decimal),
            ColumnType.Float => typeof(Double),
            ColumnType.Int => typeof(Int32),
            ColumnType.Decimal => typeof(Decimal),
            ColumnType.SmallInt => typeof(Int16),
            ColumnType.Stream => typeof(Byte[]),
            ColumnType.Uniqueidentifier => typeof(Guid),
            ColumnType.RowVersion => typeof(Byte[]),
            _ => throw new InvalidOperationException($"Invalid DataType for update. ({column.Type})"),
        };
    }

    internal static Boolean IsFieldUpdated(this TableColumn column)
    {
        return !column.Role.HasFlag(TableColumnRole.PrimaryKey)
            && !column.Role.HasFlag(TableColumnRole.Void)
            && !column.Role.HasFlag(TableColumnRole.IsFolder)
            && !column.Role.HasFlag(TableColumnRole.IsSystem)
            && column.Name != "Owner"
            && column.Type != ColumnType.RowVersion;
    }

    internal static IEnumerable<String> AllSqlFields(this TableMetadata table, IEnumerable<ReferenceMember> refFields, IEnumerable<ReferenceMember> enumFields, String alias, Boolean isDetails = false)
    {
        foreach (var c in table.Columns.Where(c => !c.IsReference && !c.IsBlob && !c.IsVoid && !c.IsEnum))
            if (c.Role.HasFlag(TableColumnRole.PrimaryKey) && !c.Role.HasFlag(TableColumnRole.RowNo))
                yield return $"[{c.Name}!!Id] = {alias}.[{c.Name}]";
            else if (c.Role.HasFlag(TableColumnRole.PrimaryKey) && c.Role.HasFlag(TableColumnRole.RowNo))
                yield return $"[{c.Name}!!RowNumber] = {alias}.[{c.Name}]";
            else if (c.Role.HasFlag(TableColumnRole.Name))
                yield return $"[{c.Name}!!Name] = {alias}.[{c.Name}]";
            else if (isDetails && c.Role.HasFlag(TableColumnRole.Kind))
                continue;
            else if (c.Role.HasFlag(TableColumnRole.RowNo))
                yield return $"[{c.Name}!!RowNumber] = {alias}.[{c.Name}]";
            else
                yield return c.IsParent ? $"Folder = {alias}.[{c.Name}]" : $"{alias}.[{c.Name}]";
        foreach (var e in enumFields)
        {
            var modelType = $"TR{e.Table.RealItemName}";
            var col = e.Column;
            var elemName = col.Name;
            yield return $"[{col.Name}!{modelType}!RefId] = {alias}.[{col.Name}]";
        }
        foreach (var c in refFields)
        {
            if (isDetails && (c.Column.Role.HasFlag(TableColumnRole.Parent) || c.Column.Role.HasFlag(TableColumnRole.Kind)))
                continue;
            var col = c.Column;
            var elemName = col.Name;
            // TR, not T - avoid recursion 
            var modelType = $"TR{c.Table.RealItemName}";
            if (col.IsParent)
            {
                elemName = "Folder";
                modelType = "TFolder";
            }
            if (c.Column.Type == ColumnType.Operation)
                yield return $"""
                    [{elemName}.Id!{modelType}!Id] = r{c.Index}.[Id], 
                    [{elemName}.Name!{modelType}!Name] = r{c.Index}.[Name],
                    [{elemName}.Url!{modelType}!] = r{c.Index}.[Url]
                    """;
            else if (c.Table.Columns.Any(c => c.Role == TableColumnRole.Done))
                yield return $"""
                    [{elemName}.{c.Table.PrimaryKeyField}!{modelType}!Id] = r{c.Index}.[{c.Table.PrimaryKeyField}], 
                    [{elemName}.{c.Table.NameField}!{modelType}!Name] = r{c.Index}.[{c.Table.NameField}],
                    [{elemName}.{c.Table.DoneField}!{modelType}!] = r{c.Index}.[{c.Table.DoneField}]
                    """;
            else
                yield return $"""
                    [{elemName}.{c.Table.PrimaryKeyField}!{modelType}!Id] = r{c.Index}.[{c.Table.PrimaryKeyField}], 
                    [{elemName}.{c.Table.NameField}!{modelType}!Name] = r{c.Index}.[{c.Table.NameField}]
                    """;
        }
    }
}

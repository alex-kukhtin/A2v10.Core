// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;

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
    public static SqlDbType ToSqlDbType(this ColumnType columnDataType)
    {
        return columnDataType switch
        {
            ColumnType.Id or ColumnType.Ref or
                ColumnType.Parent or ColumnType.Owner or
                ColumnType.User or ColumnType.Row => SqlDbType.BigInt,
            ColumnType.RowNumber => SqlDbType.Int,
            // other
            ColumnType.Operation or ColumnType.DocumentType => SqlDbType.NVarChar,
            ColumnType.Enum => SqlDbType.NVarChar,
            ColumnType.BigInt => SqlDbType.BigInt,
            ColumnType.Int => SqlDbType.Int,
            ColumnType.SmallInt or ColumnType.Direction => SqlDbType.SmallInt,
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

    public static String ToSqlDataTypeDeploy(this ColumnType columnDataType)
    {
        return columnDataType switch
        {
            ColumnType.Id or ColumnType.Ref or ColumnType.Owner or ColumnType.Document or
                ColumnType.Parent or ColumnType.User or ColumnType.Row => "platformid",
            ColumnType.IsSystem or ColumnType.IsFolder or ColumnType.Done or
                ColumnType.Void or ColumnType.Boolean => "bit",
            ColumnType.Name or ColumnType.Memo or ColumnType.Operation 
                or ColumnType.DocumentType or ColumnType.Enum or ColumnType.String  
                or ColumnType.NVarChar or ColumnType.Autonum or ColumnType.RowKind => "nvarchar",
            ColumnType.RowNumber or ColumnType.Int => "int",
            ColumnType.Date => "date",
            ColumnType.DateTime => "datetime",
            ColumnType.Money => "money",
            ColumnType.Float => "float",
            ColumnType.NChar => "nchar",
            ColumnType.Stream => "varbinary",
            ColumnType.Uniqueidentifier => "uniqueidentifier",
            ColumnType.RowVersion => "timestamp",
            ColumnType.Direction => "smallint",
            ColumnType.Decimal => $"decimal",
            _ => throw new InvalidOperationException($"Invalid ColumnType for deploy '{columnDataType}'")
        };
    }

    public static String ToSqlDataType(this ColumnType columnDataType, String maxLength = "255", Int32 scale = 0, Boolean toTableType = false)
    {
        return columnDataType switch
        {
            ColumnType.Id or ColumnType.Ref or ColumnType.Owner or ColumnType.Document or
                ColumnType.Parent or ColumnType.User or ColumnType.Row => "platformid",
            ColumnType.IsSystem or ColumnType.IsFolder or ColumnType.Done or
                ColumnType.Void or ColumnType.Boolean => "bit",
            ColumnType.Name => "nvarchar(255)",
            ColumnType.Memo => "nvarchar(255)",
            ColumnType.Operation => "nvarchar(64)",
            ColumnType.RowNumber => "int",
            ColumnType.RowKind => "nvarchar(64)",
            ColumnType.Autonum => "nvarchar(32)",
            ColumnType.DocumentType => "nvarchar(128)",
            ColumnType.Money => "money",
            ColumnType.Enum => "nvarchar(32)",
            ColumnType.String => $"nvarchar({maxLength})",
            ColumnType.NVarChar => $"nvarchar({maxLength})",
            ColumnType.NChar => $"nchar({maxLength})",
            ColumnType.Stream => $"varbinary(max)",
            ColumnType.Uniqueidentifier => "uniqueidentifier",
            ColumnType.RowVersion => toTableType ? "varbinary(8)" : "rowversion",
            ColumnType.Direction => "smallint",
            ColumnType.Decimal => $"decimal({maxLength},{scale})",
            _ => columnDataType.ToString().ToLowerInvariant(),
        };
    }

    public static String SqlDataType(this TableColumn column, Boolean toTableType = false)
    {
        var maxLength = column.MaxLength == 0 ? "max" : column.MaxLength.ToString();
        return column.Type.ToSqlDataType(maxLength, column.Scale, toTableType);
    }

    public static String SqlModelColumnName(this TableColumn column, String alias, Func<TableMetadata, String> refPredicate)
        => column.Type switch
        {
            ColumnType.Id => $"[Id!!Id] = {alias}.[Id]",
            ColumnType.Name => $"[Name!!Name] = {alias}.[Name]",
            ColumnType.RowNumber => $"[{column.Name}!!RowNumber] = {alias}.[{column.Name}]",
            ColumnType.Ref or ColumnType.Document or ColumnType.Operation =>
                $"[{column.Name}!{refPredicate(column.RefTableCheck)}!RefId] = {alias}.[{column.Name}]",
            _ => $"{alias}.[{column.Name}]"
        };

    public static Type ClrDataType(this TableColumn column)
    {
        return column.Type switch
        {
            ColumnType.Id or ColumnType.Ref or ColumnType.Owner or ColumnType.Parent or ColumnType.Row => typeof(Int64),
            ColumnType.Name or ColumnType.Memo or ColumnType.Autonum => typeof(String),
            ColumnType.RowNumber => typeof(Int32),
            ColumnType.RowKind => typeof(String),
            ColumnType.Operation => typeof(String),
            ColumnType.Enum => typeof(String),
            ColumnType.BigInt => typeof(Int64),
            ColumnType.String or ColumnType.NVarChar or
                ColumnType.NChar or ColumnType.DocumentType => typeof(String),
            ColumnType.Date or ColumnType.DateTime => typeof(DateTime),
            ColumnType.Bit or ColumnType.Done or ColumnType.Void
                or ColumnType.IsFolder or ColumnType.IsSystem => typeof(Boolean),
            ColumnType.Money => typeof(Decimal),
            ColumnType.Float => typeof(Double),
            ColumnType.Int => typeof(Int32),
            ColumnType.Decimal => typeof(Decimal),
            ColumnType.SmallInt or ColumnType.Direction => typeof(Int16),
            ColumnType.Stream => typeof(Byte[]),
            ColumnType.Uniqueidentifier => typeof(Guid),
            ColumnType.RowVersion => typeof(Byte[]),
            _ => throw new InvalidOperationException($"Invalid DataType for update. ({column.Type})"),
        };
    }

    internal static Boolean IsFieldUpdated(this TableColumn column)
    {
        return column.Type != ColumnType.Id
            && column.Type != ColumnType.Void
            && column.Type != ColumnType.IsFolder
            && column.Type != ColumnType.Done
            && column.Type != ColumnType.Operation
            && column.Type != ColumnType.IsSystem
            && column.Type != ColumnType.Owner
            && column.Type != ColumnType.Parent
            && column.Type != ColumnType.RowVersion;
    }
    internal static Boolean IsFieldInserted(this TableColumn column)
    {
        return column.IsFieldUpdated() || column.Type == ColumnType.Operation;
    }
}

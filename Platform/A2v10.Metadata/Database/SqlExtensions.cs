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
                ColumnType.Parent or ColumnType.Owner or ColumnType.Folder or
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
                ColumnType.Parent or ColumnType.Folder or ColumnType.User or ColumnType.Row => "platformid",
            ColumnType.IsSystem or ColumnType.Done or
                ColumnType.Void or ColumnType.Boolean => "bit",
            ColumnType.Name or ColumnType.Memo or ColumnType.Operation 
                or ColumnType.DocumentType or ColumnType.Enum or ColumnType.String  
                or ColumnType.NVarChar or ColumnType.Autonum or ColumnType.RowKind 
                or ColumnType.Color => "nvarchar",
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

    /* Physical facets of a column - what INFORMATION_SCHEMA.COLUMNS is expected to
     * return. A single source for both the DDL and the a2meta.Columns seed: should the
     * two ever diverge, SyncSchema would emit an ALTER on every single run.
     */

    // CHARACTER_MAXIMUM_LENGTH: in characters, -1 means max.
    // Filled in ONLY for types that actually have a length; null for the rest - and it
    // has to be compared just as selectively. Putting the catalog's "reference" values
    // here for the other types would mean inventing defaults just to simplify one query.
    public static Int32? DeployLength(this ColumnType columnDataType)
    {
        return columnDataType switch
        {
            ColumnType.Name or ColumnType.Memo => 255,
            ColumnType.Operation or ColumnType.RowKind => 64,
            ColumnType.Autonum or ColumnType.Enum or ColumnType.Color => 32,
            ColumnType.DocumentType => 128,
            ColumnType.String or ColumnType.NVarChar or ColumnType.NChar => 255,
            ColumnType.Stream or ColumnType.VarBinary => -1,
            _ => null
        };
    }

    public static Int32? DeployLength(this TableColumn column)
        => column.Type switch
        {
            // the author may set a length only where the type accepts one at all
            ColumnType.String or ColumnType.NVarChar or ColumnType.NChar
                => column.Length ?? column.Type.DeployLength(),
            _ => column.Type.DeployLength()
        };

    // NUMERIC_PRECISION / NUMERIC_SCALE: decimal only - the one type whose precision the
    // author sets. Null for the rest: the catalog does have values there, but there is
    // nothing for us to compare, since the declaration cannot change them anyway.
    public static Int32? DeployPrecision(this TableColumn column)
        => column.Type == ColumnType.Decimal ? column.Precision ?? 19 : null;

    public static Int32? DeployScale(this TableColumn column)
        => column.Type == ColumnType.Decimal ? column.Scale ?? 4 : null;

    /* A ready-made SQL default expression. Takes NO part in comparison - it exists for
     * exactly one purpose, to be substituted into an add column, because
     * 'add column [Void] bit not null' would fail without it.
     * There is no Id default here and there cannot be: a sequence only ever sits on the
     * primary key, and that appears together with the table, never via add column.
     */
    public static String? DeployDefault(this TableColumn column)
        => column.HasDefaultBit ? "0" : null;

    // IS_NULLABLE. Must match whatever CreateTable emits.
    // RowVersion is listed here not because we ask for not null, but because SQL Server
    // applies it itself: timestamp is an exception to the usual nullable default.
    public static Boolean DeployNullable(this TableColumn column)
        => !(column.Type == ColumnType.Id
            || column.Type == ColumnType.Owner
            || column.Type == ColumnType.RowVersion
            || column.HasDefaultBit
            || column.Required);

    public static String ToSqlDataType(this ColumnType columnDataType, Int32? length = null, Int32? precision = null, Int32? scale = null, Boolean toTableType = false)
    {
        var len = length ?? columnDataType.DeployLength();
        var lenStr = len == -1 ? "max" : len?.ToString() ?? "255";
        return columnDataType switch
        {
            ColumnType.Id or ColumnType.Ref or ColumnType.Owner or ColumnType.Document or
                ColumnType.Parent or ColumnType.Folder or ColumnType.User or ColumnType.Row => "platformid",
            ColumnType.IsSystem or ColumnType.Done or
                ColumnType.Void or ColumnType.Boolean => "bit",
            ColumnType.Name or ColumnType.Memo or ColumnType.Operation or
                ColumnType.RowKind or ColumnType.Autonum or ColumnType.DocumentType or
                ColumnType.Enum or ColumnType.String or ColumnType.Color or
                ColumnType.NVarChar => $"nvarchar({lenStr})",
            ColumnType.RowNumber => "int",
            ColumnType.Money => "money",
            ColumnType.NChar => $"nchar({lenStr})",
            ColumnType.Stream => $"varbinary(max)",
            ColumnType.Uniqueidentifier => "uniqueidentifier",
            ColumnType.RowVersion => toTableType ? "varbinary(8)" : "rowversion",
            ColumnType.Direction => "smallint",
            ColumnType.Decimal => $"decimal({precision ?? 19},{scale ?? 4})",
            _ => columnDataType.ToString().ToLowerInvariant(),
        };
    }

    public static String SqlDataType(this TableColumn column, Boolean toTableType = false)
    {
        return column.Type.ToSqlDataType(column.DeployLength(), column.Precision, column.Scale, toTableType);
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
        // TODO: ID REF TYPE
        return column.Type switch
        {
            ColumnType.Id or ColumnType.Ref or ColumnType.Owner or 
                ColumnType.Parent or ColumnType.Folder or ColumnType.Row => typeof(Int64),
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
                or ColumnType.IsSystem => typeof(Boolean),
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

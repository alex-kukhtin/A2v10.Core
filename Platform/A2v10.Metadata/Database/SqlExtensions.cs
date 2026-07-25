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

    /* Фізичні фасети колонки — те, що має повернути INFORMATION_SCHEMA.COLUMNS.
     * Єдине джерело і для DDL, і для seed'у a2meta.Columns: якщо вони розійдуться,
     * SyncSchema генеруватиме ALTER на кожному прогоні.
     */

    // CHARACTER_MAXIMUM_LENGTH: у символах, -1 = max.
    // Заповнюється ТІЛЬКИ там, де тип має довжину; решті null — і звіряти її треба
    // так само вибірково. Ставити сюди «еталонні» значення каталогу для інших типів
    // означало б вигадувати умовчання заради спрощення одного запиту.
    public static Int32? DeployLength(this ColumnType columnDataType)
    {
        return columnDataType switch
        {
            ColumnType.Name or ColumnType.Memo => 255,
            ColumnType.Operation or ColumnType.RowKind => 64,
            ColumnType.Autonum or ColumnType.Enum => 32,
            ColumnType.DocumentType => 128,
            ColumnType.String or ColumnType.NVarChar or ColumnType.NChar => 255,
            ColumnType.Stream or ColumnType.VarBinary => -1,
            _ => null
        };
    }

    public static Int32? DeployLength(this TableColumn column)
        => column.Type switch
        {
            // довжину задає автор тільки там, де тип її взагалі приймає
            ColumnType.String or ColumnType.NVarChar or ColumnType.NChar
                => column.Length ?? column.Type.DeployLength(),
            _ => column.Type.DeployLength()
        };

    // NUMERIC_PRECISION / NUMERIC_SCALE: тільки decimal — єдиний тип, де точність
    // задає автор. Решті null: каталог має там свої значення, але звіряти нам нічого,
    // бо змінити їх декларація не може.
    public static Int32? DeployPrecision(this TableColumn column)
        => column.Type == ColumnType.Decimal ? column.Precision ?? 19 : null;

    public static Int32? DeployScale(this TableColumn column)
        => column.Type == ColumnType.Decimal ? column.Scale ?? 4 : null;

    /* Готовий SQL-вираз дефолта. У порівнянні НЕ бере участі — потрібен рівно для
     * add column, бо 'add column [Void] bit not null' без нього впаде.
     * Дефолта Id тут немає і бути не може: sequence сидить тільки на первинному ключі,
     * а він з'являється разом із таблицею, через add column — ніколи.
     */
    public static String? DeployDefault(this TableColumn column)
        => column.HasDefaultBit ? "0" : null;

    // IS_NULLABLE. Мусить збігатися з тим, що ставить CreateTable.
    // RowVersion тут не тому, що ми просимо not null, а тому, що SQL Server ставить
    // його сам: timestamp — виняток із загального умовчання про nullable (перевірено).
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
                ColumnType.Parent or ColumnType.User or ColumnType.Row => "platformid",
            ColumnType.IsSystem or ColumnType.IsFolder or ColumnType.Done or
                ColumnType.Void or ColumnType.Boolean => "bit",
            ColumnType.Name or ColumnType.Memo or ColumnType.Operation or
                ColumnType.RowKind or ColumnType.Autonum or ColumnType.DocumentType or
                ColumnType.Enum or ColumnType.String or ColumnType.NVarChar => $"nvarchar({lenStr})",
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

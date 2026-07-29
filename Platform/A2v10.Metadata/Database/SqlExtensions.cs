// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;

namespace A2v10.Metadata;

internal sealed record SqlDbTypeInfo(SqlDbType SqlDbType, Type ClrType, String SqlName, Int32? Length = null, Int32? Precision = null, Int32? Scale = null)
{
    // Not stored: it follows from the name plus the facet the type carries, and a stored
    // copy would be free to drift from it.
    public String SqlFullName => SqlDbType switch
    {
        SqlDbType.NVarChar or SqlDbType.NChar or SqlDbType.VarChar or SqlDbType.Char or
            SqlDbType.VarBinary or SqlDbType.Binary
                => Length == -1 ? $"{SqlName}(max)" : $"{SqlName}({Length})",
        SqlDbType.Decimal => $"{SqlName}({Precision}, {Scale})",
        _ => SqlName
    };
}

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

    // -1 (max) travels through untouched: it is the catalog's own notation, and
    // SqlFullName renders it.
    private static Int32 ToColumnLength(this Int32? length) => length ?? 255;

    /* THE single dispatch over ColumnType. Every SQL-side facet of a domain comes from
     * here, and everything below is a one-liner over it - so disagreeing with it is not
     * possible any more. The facets the author may set arrive as arguments: that is all
     * the switch ever needed from a column.
     */
    public static SqlDbTypeInfo ToSqlDbTypeInfo(this ColumnType columnType,
        Int32? length = null, Int32? precision = null, Int32? scale = null)
        => columnType switch
        {
            // id + references: every one of them is platformid, the FK carries the meaning
            ColumnType.Id or ColumnType.Ref or ColumnType.Owner or ColumnType.Parent or
                ColumnType.Folder or ColumnType.Row or ColumnType.Company or
                ColumnType.User or ColumnType.Document
                    => new SqlDbTypeInfo(SqlDbType.BigInt, typeof(Int64), "platformid"),
            // bit
            ColumnType.IsSystem or ColumnType.Void or ColumnType.Done or
                ColumnType.Bit or ColumnType.Boolean
                    => new SqlDbTypeInfo(SqlDbType.Bit, typeof(Boolean), "bit"),
            // integers
            ColumnType.Direction => new SqlDbTypeInfo(SqlDbType.SmallInt, typeof(Int16), "smallint"),
            ColumnType.RowNumber or ColumnType.Integer => new SqlDbTypeInfo(SqlDbType.Int, typeof(Int32), "int"),
            ColumnType.BigInt => new SqlDbTypeInfo(SqlDbType.BigInt, typeof(Int64), "bigint"),
            // rowversion: INFORMATION_SCHEMA reports 'timestamp', so that is the facet.
            // The DDL spelling ('rowversion') and the TVP one ('varbinary(8)') belong to
            // the caller - see ToSqlDataType(toTableType).
            ColumnType.RowVersion => new SqlDbTypeInfo(SqlDbType.Timestamp, typeof(Byte[]), "timestamp"),
            // date
            ColumnType.Date => new SqlDbTypeInfo(SqlDbType.Date, typeof(DateTime), "date"),
            ColumnType.DateTime => new SqlDbTypeInfo(SqlDbType.DateTime, typeof(DateTime), "datetime"),
            // strings whose length the author may set
            ColumnType.String
                => new SqlDbTypeInfo(SqlDbType.NVarChar, typeof(String), "nvarchar", length.ToColumnLength()),
            ColumnType.NChar => new SqlDbTypeInfo(SqlDbType.NChar, typeof(String), "nchar", length.ToColumnLength()),
            // strings whose length the domain fixes
            ColumnType.Name or ColumnType.Memo => new SqlDbTypeInfo(SqlDbType.NVarChar, typeof(String), "nvarchar", 255),
            ColumnType.DocumentType => new SqlDbTypeInfo(SqlDbType.NVarChar, typeof(String), "nvarchar", 128),
            // discriminators - one length for all of them
            ColumnType.Operation or ColumnType.RowKind or
                ColumnType.Autonum or ColumnType.Enum
                    => new SqlDbTypeInfo(SqlDbType.NVarChar, typeof(String), "nvarchar", 64),
            ColumnType.Color => new SqlDbTypeInfo(SqlDbType.NVarChar, typeof(String), "nvarchar", 32),
            // numbers with business semantics: precision is 19 throughout, only scale varies
            ColumnType.Amount => new SqlDbTypeInfo(SqlDbType.Decimal, typeof(Decimal), "decimal", null, 19, 4),
            ColumnType.Price or ColumnType.Qty or
                ColumnType.Percent => new SqlDbTypeInfo(SqlDbType.Decimal, typeof(Decimal), "decimal", null, 19, 6),
            ColumnType.Factor => new SqlDbTypeInfo(SqlDbType.Decimal, typeof(Decimal), "decimal", null, 19, 8),
            // numbers without: the author sets the precision himself
            ColumnType.Decimal => new SqlDbTypeInfo(SqlDbType.Decimal, typeof(Decimal), "decimal", null, precision ?? 19, scale ?? 4),
            ColumnType.Money => new SqlDbTypeInfo(SqlDbType.Money, typeof(Decimal), "money"),
            ColumnType.Float => new SqlDbTypeInfo(SqlDbType.Float, typeof(Double), "float"),
            // binary
            ColumnType.Stream or ColumnType.VarBinary => new SqlDbTypeInfo(SqlDbType.VarBinary, typeof(Byte[]), "varbinary", -1),
            ColumnType.Uniqueidentifier => new SqlDbTypeInfo(SqlDbType.UniqueIdentifier, typeof(Guid), "uniqueidentifier"),
            _ => throw new InvalidOperationException($"SqlDbTypeInfo. Invalid type '{columnType}'")
        };

    public static SqlDbTypeInfo ToSqlDbTypeInfo(this TableColumn column)
        => column.Type.ToSqlDbTypeInfo(column.Length, column.Precision, column.Scale);

    public static String ToSqlDataTypeDeploy(this ColumnType columnDataType)
        => columnDataType.ToSqlDbTypeInfo().SqlName;

    /* Physical facets of a column - what INFORMATION_SCHEMA.COLUMNS is expected to
     * return. A single source for both the DDL and the a2meta.Columns seed: should the
     * two ever diverge, SyncSchema would emit an ALTER on every single run.
     *
     * All three come straight off the type descriptor now. Precision and scale used to be
     * blanked for everything but ColumnType.Decimal, on the grounds that the declaration
     * cannot change them - which is true of the author and false of the platform. A null
     * there costs the seed its whole purpose: a column left at decimal(19,2) would match
     * on the type name alone and never be corrected, and 'money -> decimal(19,4)' could
     * not even name its target. The catalog's own precision for int/bigint must simply be
     * compared selectively, by type - not erased here, where it is the thing we need.
     */
    public static Int32? DeployLength(this TableColumn column)
        => column.ToSqlDbTypeInfo().Length;

    public static Int32? DeployPrecision(this TableColumn column)
        => column.ToSqlDbTypeInfo().Precision;

    public static Int32? DeployScale(this TableColumn column)
        => column.ToSqlDbTypeInfo().Scale;

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

    /* RowVersion is the one type the descriptor cannot answer for: the catalog calls it
     * 'timestamp', the DDL wants 'rowversion' and a table type wants 'varbinary(8)'.
     * Which of the three applies is the caller's context, not a property of the type, so
     * it stays here as an explicit exception rather than being forced into the record.
     */
    public static String ToSqlDataType(this ColumnType columnDataType, Int32? length = null, Int32? precision = null, Int32? scale = null, Boolean toTableType = false)
        => columnDataType == ColumnType.RowVersion
            ? (toTableType ? "varbinary(8)" : "rowversion")
            : columnDataType.ToSqlDbTypeInfo(length, precision, scale).SqlFullName;

    public static String SqlDataType(this TableColumn column, Boolean toTableType = false)
        => column.Type.ToSqlDataType(column.Length, column.Precision, column.Scale, toTableType);

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
        => column.ToSqlDbTypeInfo().ClrType;

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

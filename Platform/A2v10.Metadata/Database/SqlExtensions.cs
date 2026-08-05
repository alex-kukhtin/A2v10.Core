// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

/* SQL-side facets of a domain, and nothing else. Every field here is invariant to the
 * identifier base the application runs on: 'platformid' is spelled the same whether it
 * sits on bigint or on uniqueidentifier, and SQL Server itself carries the alias name
 * through INFORMATION_SCHEMA.COLUMNS.DOMAIN_NAME. Anything that DOES depend on that base
 * lives in AppPlatformId - keeping it out of here is what makes this record answerable
 * without knowing which database we are on.
 */
internal sealed record SqlDbTypeInfo(String SqlName, Int32? Length = null, Int32? Precision = null, Int32? Scale = null)
{
    // Not stored: it follows from the name plus the facet the type carries, and a stored
    // copy would be free to drift from it. Which facet is present decides the spelling -
    // there is no second enum to disagree with.
    public String SqlFullName =>
        Length.HasValue ? (Length == -1 ? $"{SqlName}(max)" : $"{SqlName}({Length})")
        : Precision.HasValue ? $"{SqlName}({Precision}, {Scale})"
        : SqlName;
}

/* The base the platform identifier rests on. Chosen once per database and never changed
 * afterwards, so it is resolved once per data source and passed down - never re-derived,
 * and never defaulted: a wrong guess here would not fail, it would quietly write the
 * wrong value into a live database.
 * It is read from the database itself (the base of the 'platformid' alias), so it is a
 * fact rather than a declaration - there is no second place saying what platformid is,
 * and therefore nothing to drift from the schema. The vocabulary is the SQL type name,
 * the same one SqlDbTypeInfo.SqlName speaks, so no third enum stands in between.
 */
internal sealed record AppPlatformId(Type ClrType)
{
    public static AppPlatformId FromSqlName(String sqlName) => sqlName switch
    {
        "bigint" => new(typeof(Int64)),
        "int" => new(typeof(Int32)),
        "uniqueidentifier" => new(typeof(Guid)),
        _ => throw new InvalidOperationException($"AppPlatformId. Unsupported 'platformid' base type '{sqlName}'")
    };

    /* An identifier that references nothing. Recognised by shape rather than compared
     * against one stored empty value: what arrives here has been through an ExpandoObject
     * and is loosely typed - the same integer id turns up as Int32 or Int64 depending on
     * how it was parsed - so an Equals against a boxed value would answer false for
     * exactly the case being asked about, and the empty reference would be written as a
     * real foreign key. The arms that cannot occur on a given base are simply never hit.
     */
    public static Boolean IsEmpty(Object? value) => value switch
    {
        null => true,
        Int64 i64 => i64 == 0,
        Int32 i32 => i32 == 0,
        Guid g => g == Guid.Empty,
        _ => false
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
            // id + references: every one of them is platformid, the FK carries the meaning.
            // The base it rests on is deliberately absent - see AppPlatformId.
            ColumnType.Id or ColumnType.Ref or ColumnType.Owner or ColumnType.Parent or
                ColumnType.Folder or ColumnType.Row or ColumnType.Company or
                ColumnType.User or ColumnType.Document
                    => new SqlDbTypeInfo("platformid"),
            // bit
            ColumnType.IsSystem or ColumnType.Void or ColumnType.Done or
                ColumnType.Bit or ColumnType.Boolean
                    => new SqlDbTypeInfo("bit"),
            // integers
            ColumnType.Direction => new SqlDbTypeInfo("smallint"),
            ColumnType.RowNumber or ColumnType.Integer => new SqlDbTypeInfo("int"),
            ColumnType.BigInt => new SqlDbTypeInfo("bigint"),
            // rowversion: INFORMATION_SCHEMA reports 'timestamp', so that is the facet.
            // The DDL spelling ('rowversion') and the TVP one ('varbinary(8)') belong to
            // the caller - see ToSqlDataType(toTableType).
            ColumnType.RowVersion => new SqlDbTypeInfo("timestamp"),
            // date
            ColumnType.Date => new SqlDbTypeInfo("date"),
            ColumnType.DateTime => new SqlDbTypeInfo("datetime"),
            // strings whose length the author may set
            ColumnType.String => new SqlDbTypeInfo("nvarchar", length.ToColumnLength()),
            ColumnType.NChar => new SqlDbTypeInfo("nchar", length.ToColumnLength()),
            // strings whose length the domain fixes
            ColumnType.Name or ColumnType.Memo => new SqlDbTypeInfo("nvarchar", 255),
            ColumnType.DocumentType => new SqlDbTypeInfo("nvarchar", 128),
            // discriminators - one length for all of them
            ColumnType.Operation or ColumnType.RowKind or
                ColumnType.Autonum or ColumnType.Enum
                    => new SqlDbTypeInfo("nvarchar", 64),
            ColumnType.Color => new SqlDbTypeInfo("nvarchar", 32),
            // numbers with business semantics: precision is 19 throughout, only scale varies
            ColumnType.Amount => new SqlDbTypeInfo("decimal", null, 19, 4),
            ColumnType.Price or ColumnType.Qty or
                ColumnType.Percent => new SqlDbTypeInfo("decimal", null, 19, 6),
            ColumnType.Factor => new SqlDbTypeInfo("decimal", null, 19, 8),
            // numbers without: the author sets the precision himself
            ColumnType.Decimal => new SqlDbTypeInfo("decimal", null, precision ?? 19, scale ?? 4),
            ColumnType.Money => new SqlDbTypeInfo("money"),
            ColumnType.Float => new SqlDbTypeInfo("float"),
            // binary
            ColumnType.Stream or ColumnType.VarBinary => new SqlDbTypeInfo("varbinary", -1),
            ColumnType.Uniqueidentifier => new SqlDbTypeInfo("uniqueidentifier"),
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
            || column.HasDefaultBit);

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
                $"[{column.Name}!{refPredicate(column.RefTableCheck.Storage)}!RefId] = {alias}.[{column.Name}]",
            _ => $"{alias}.[{column.Name}]"
        };

    /* The one place a domain has to be answered for in CLR terms, for the one consumer
     * that cannot use a SQL type name: a DataColumn wants a System.Type. SQL Server keeps
     * 'platformid' nominal all the way through (DDL, table types, DOMAIN_NAME), ADO.NET
     * has no such notion - so this is where the alias is resolved to its base, and the
     * only reason AppPlatformId has to travel at all.
     * Keyed on the SQL name rather than on ColumnType: CLR type is a function of the SQL
     * type, so there is no second table of domains to fall out of step with this one.
     */
    public static Type ClrDataType(this TableColumn column, AppPlatformId platformId)
        => column.ToSqlDbTypeInfo().SqlName switch
        {
            "platformid" => platformId.ClrType,
            "nvarchar" or "nchar" => typeof(String),
            "bit" => typeof(Boolean),
            "smallint" => typeof(Int16),
            "int" => typeof(Int32),
            "bigint" => typeof(Int64),
            "date" or "datetime" => typeof(DateTime),
            "decimal" or "money" => typeof(Decimal),
            "float" => typeof(Double),
            "varbinary" or "timestamp" => typeof(Byte[]),
            "uniqueidentifier" => typeof(Guid),
            var sqlName => throw new InvalidOperationException($"ClrDataType. No CLR type for '{sqlName}'")
        };

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

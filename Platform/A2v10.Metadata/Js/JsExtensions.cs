// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class JsExtensions
{
    /* The TS type of a column as the browser sees it. Keyed on the SQL name, as ClrDataType is:
     * the TS type is a function of the SQL type, so there is no second table of domains to fall
     * out of step with ToSqlDbTypeInfo - and a ColumnType added there is covered here by
     * construction. 'platformid' is the one name that needs the base the database rests on:
     * a bigint arrives as a number, a uniqueidentifier as a string.
     *
     * No arm for a reference of any kind. A ref column never reaches here: ScriptBuilder asks
     * IsRef first and writes the TARGET's type name, which is what the model actually carries.
     */
    public static String ToTsType(this TableColumn column, AppPlatformId platformId)
        => column.ToSqlDbTypeInfo().SqlName switch
        {
            "platformid" => platformId.ClrType == typeof(Guid) ? "string" : "number",
            "nvarchar" or "nchar" or "uniqueidentifier" or "varbinary" or "timestamp" => "string",
            "bit" => "boolean",
            "smallint" or "int" or "bigint" or "decimal" or "money" or "float" => "number",
            "date" or "datetime" => "Date",
            var sqlName => throw new InvalidOperationException($"ToTsType. No TS type for '{sqlName}'")
        };
}

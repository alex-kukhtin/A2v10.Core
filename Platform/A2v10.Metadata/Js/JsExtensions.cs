// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class JsExtensions
{
    private const String TSString = "string";
    private const String TSNumber = "number";
    private const String TBoolean = "boolean";
    private const String TSDate = "Date";
    public static String ToTsType(this ColumnType columnDataType)
    {
        return columnDataType switch
        {
            ColumnType.Id => TSNumber,
            ColumnType.String => TSString,
            ColumnType.Money => TSNumber,
            ColumnType.Boolean => TBoolean,
            ColumnType.Integer or ColumnType.Number => TSNumber,
            ColumnType.DateTime 
                or ColumnType.Date => TSDate,
            /* No arm for a reference of any kind. A ref column never reaches here: ScriptBuilder
             * asks IsRef first and writes the TARGET's type name, which is what the model actually
             * carries. An arm here would be a second answer to that question, reachable only by
             * someone calling this directly - and it used to give 'number' for every reference,
             * including the ones keyed by a code.
             */
            _ => throw new InvalidOperationException($"Unknown TS DataType {columnDataType}")
        };
    }
}

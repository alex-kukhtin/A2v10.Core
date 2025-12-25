// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class JsExtensions
{
    private const String TSString = "string";
    private const String TSNumber = "number";
    private const String TBoolean = "boolean";
    public static String ToTsType(this ColumnDataType columnDataType, ColumnDataType idDataType)
    {
        var idType = idDataType switch { 
            ColumnDataType.BigInt => TSNumber,
            ColumnDataType.String or ColumnDataType.Uniqueidentifier => TSString,
            _ => throw new InvalidOperationException($"Unknown TS Id DataType {idDataType}")
        };
        return columnDataType switch
        {
            ColumnDataType.Id => idType,
            ColumnDataType.String or ColumnDataType.NChar or ColumnDataType.Operation
                or ColumnDataType.NVarChar => TSString,
            ColumnDataType.Float or ColumnDataType.Money => TSNumber,
            ColumnDataType.Bit => TBoolean,
            ColumnDataType.Enum => TSString, // TODO: enumerable
            ColumnDataType.Reference => idType, // TODO: reference
            _ => throw new InvalidOperationException($"Unknown TS DataType {columnDataType}")
        };
    }
}

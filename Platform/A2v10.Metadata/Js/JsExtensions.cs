// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class JsExtensions
{
    private const String TSString = "string";
    private const String TSNumber = "number";
    private const String TBoolean = "boolean";
    private const String TSDate = "Date";
    public static String ToTsType(this ColumnType columnDataType, ColumnType idDataType)
    {
        var idType = idDataType switch { 
            ColumnType.BigInt => TSNumber,
            ColumnType.String or ColumnType.Uniqueidentifier => TSString,
            _ => throw new InvalidOperationException($"Unknown TS Id DataType {idDataType}")
        };
        return columnDataType switch
        {
            ColumnType.Id => idType,
            ColumnType.String or ColumnType.NChar or ColumnType.Operation
                or ColumnType.NVarChar => TSString,
            ColumnType.Float or ColumnType.Money => TSNumber,
            ColumnType.Bit => TBoolean,
            ColumnType.BigInt or ColumnType.Int => TSNumber,
            ColumnType.DateTime 
                or ColumnType.Date => TSDate,
            ColumnType.Enum => TSString, // TODO: enumerable
            ColumnType.Ref => idType, // TODO: reference
            _ => throw new InvalidOperationException($"Unknown TS DataType {columnDataType}")
        };
    }
}

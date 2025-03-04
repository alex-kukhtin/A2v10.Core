// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using A2v10.Infrastructure;

namespace A2v10.Metadata.SqlServer;

public enum ColumnDataType
{
    BigInt,
    Int,
    String,
    Boolean,
    Date,
    DateTime,
    Currency,
    Float,
    Guid
}

public record ColumnReference
{
    public String RefSchema { get; init; } = default!;
    public String RefTable { get; init; } = default!;
    public String RefColumn { get; init; } = default!;
    public String ModelType => $"TR{RefTable.Singular()}";
}
public record TableColumn
{
    #region Database Fields
    public String Name { get; init; } = default!;
    public String DataType { get; init; } = default!;
    public Int32? MaxLength { get; init; }
    public ColumnReference? Reference { get; init; }
    #endregion

    internal Boolean IsReference => Reference != null;    
    internal Boolean IsParent => Name == "Parent";

    internal Boolean IsSearchable => ColumnDataType == ColumnDataType.String;

    internal ColumnDataType ColumnDataType => DataType switch
        {
            "bigint" => ColumnDataType.BigInt,
            "Int" => ColumnDataType.Int,
            "nvarchar" => ColumnDataType.String,
            "date" => ColumnDataType.Date,
            "datetime" => ColumnDataType.DateTime,
            "money" => ColumnDataType.Currency,
            "float" => ColumnDataType.Float,   
            "bit" => ColumnDataType.Boolean,
            "uniqueidentifier" => ColumnDataType.Guid,
            _ => throw new InvalidOperationException($"Unknown DataType: {DataType}")
        };
}
public record TableMetadata
{
    public List<TableColumn> Columns { get; } = [];

    public String Schema { get; init; } = default!;
    public String Table { get; init; } = default!;

    public String SqlTableName => $"{Schema}.[{Table}]";
    public String ModelType => $"T{Table.Singular()}";

    internal IEnumerable<TableColumn> RealColumns(IModelJsonMeta meta)
    {
        return Columns.Where(c => c.Name != meta.Void && !meta.Hidden.Any(h => h == c.Name));
    }

    internal List<(TableColumn Column, Int32 Index)> RefFields(IModelJsonMeta meta)
    {
        var index = 0;
        return RealColumns(meta).Where(c => c.IsReference).Select(c => (Column: c, Index: ++index)).ToList();
    }

    internal IEnumerable<String> SelectFieldsAll(String alias, IModelJsonMeta meta, List<(TableColumn Column, Int32 Index)> refFields)
    {
        foreach (var c in RealColumns(meta).Where(c => !c.IsReference))
            if (c.Name == "Id")
                yield return $"[Id!!Id] = {alias}.[{c.Name}]";
            else
                yield return c.IsParent ? $"ParentElem = {alias}.[{c.Name}]" : $"{alias}.[{c.Name}]";
        foreach (var c in refFields)
        {
            var col = c.Column;
            var colRef = c.Column.Reference!;
            var sf = String.Empty;
            if (col.IsParent)
                sf = "Elem";
            yield return $"[{col.Name}{sf}.Id!{colRef.ModelType}!Id] = r{c.Index}.[Id], [{col.Name}{sf}.Name!{colRef.ModelType}!Name] = r{c.Index}.[Name]";
        }
    }
}

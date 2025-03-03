// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata.SqlServer;

public record ColumnReference
{
    public String RefSchema { get; init; } = default!;
    public String RefTable { get; init; } = default!;
    public String RefColumn { get; init; } = default!;
}
public record TableColumn
{
    #region Database Fields
    public String Name { get; init; } = default!;
    public String DataType { get; init; } = default!;
    public Int32? MaxLength { get; init; }
    public ColumnReference? Reference { get; init; }
    #endregion

    internal String SqlFieldName(String alias) => Name == "Parent" ? $"ParentElem = {alias}.[{Name}]" : $"{alias}.[{Name}]";
    internal Boolean IsReference => Reference != null;    
}
public record TableMetadata
{
    public List<TableColumn> Columns { get; } = [];

    public String Schema { get; init; } = default!;
    public String Table { get; init; } = default!;

    public String SqlTableName => $"{Schema}.[{Table}]";
    public String ModelType => $"T{Table.Singular()}";

    public IEnumerable<TableColumn> RealColumns(IModelJsonMeta meta)
    {
        return Columns.Where(c => c.Name != meta.Void && !meta.Hidden.Any(h => h == c.Name));
    }
}

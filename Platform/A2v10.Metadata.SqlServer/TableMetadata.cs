// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using A2v10.Infrastructure;
using A2v10.Xaml;

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
    public TableMetadata RefMetadata { get; set; } = new();
    public String EndpointPath { get; set; } = default!;
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
            "int" => ColumnDataType.Int,
            "nvarchar" => ColumnDataType.String,
            "nchar" => ColumnDataType.String,
            "date" => ColumnDataType.Date,
            "datetime" => ColumnDataType.DateTime,
            "money" => ColumnDataType.Currency,
            "float" => ColumnDataType.Float,   
            "bit" => ColumnDataType.Boolean,
            "uniqueidentifier" => ColumnDataType.Guid,
            _ => throw new InvalidOperationException($"Unknown DataType: {DataType}")
        };
}

public record ViewColumn
{
    public TableColumn Column { get; init; } = default!;
    public String Name { get; set; } = default!;
    public String Header { get; init; } = default!;

    public static ViewColumn FromString(String def)
    {
        if (String.IsNullOrWhiteSpace(def))
            throw new InvalidOperationException("empty column ");
        var spl = def.Trim().Split(':');
        var name = spl[0];
        if (String.IsNullOrEmpty(name))
            throw new InvalidOperationException("invalid column name");
        var header = spl.Length > 1 ? spl[1] : $"@[{name}]";
        return new ViewColumn()
        {
            Name = name,
            Header = header
        };
    }
}

public record TableDefinition
{
    public String? Void { get; init; }
    public String? Name { get; init; }
    public String? Id { get; init; }
    public String? IsFolder { get; init; }
    public String? HiddenColumns { get; init; }

    public String VoidField => Void ?? "Void";
    public String NameField => Name ?? "Name";
    public String IdField => Id ?? "Id";
    public String IsFolderField => IsFolder ?? "IsFolder";
}
public record TableMetadata
{
    public List<TableColumn> Columns { get; } = [];

    public String Schema { get; init; } = default!;
    public String Table { get; init; } = default!;
    public TableDefinition Definition { get; set; } = default!;
    public String SqlTableName => $"{Schema}.[{Table}]";
    public String ModelType => $"T{Table.Singular()}";

    internal IEnumerable<ViewColumn> EditColumns(IModelBaseMeta meta)
    {
        return IndexColumns(meta);
    }

    internal IEnumerable<ViewColumn> IndexColumns(IModelBaseMeta meta)
    {
        var tableColumns = Columns.Select(c => new ViewColumn() { Column = c, Name = c.Name, Header = $"@[{c.Name}]" });
        if (String.IsNullOrEmpty(meta.Columns))
            return tableColumns;
        var viewColumns = meta.Columns.Split(',').Select(c => ViewColumn.FromString(c));
        return viewColumns.Join(tableColumns, v => v.Name, t => t.Name, (v, t) => new ViewColumn() {Name = t.Name, Header = v.Header, Column = t.Column });
    }

    internal List<(TableColumn Column, Int32 Index)> RefFields()
    {
        var index = 0;
        return Columns.Where(c => c.IsReference).Select(c => (Column: c, Index: ++index)).ToList();
    }

    internal IEnumerable<String> SelectFieldsAll(String alias, List<(TableColumn Column, Int32 Index)> refFields)
    {
        foreach (var c in Columns.Where(c => !c.IsReference))
            if (c.Name == Definition.IdField)
                yield return $"[{c.Name}!!Id] = {alias}.[{c.Name}]";
            else
                yield return c.IsParent ? $"ParentElem = {alias}.[{c.Name}]" : $"{alias}.[{c.Name}]";
        foreach (var c in refFields)
        {
            var col = c.Column;
            var colRef = c.Column.Reference!;
            var sf = String.Empty;
            if (col.IsParent)
                sf = "Elem";
            var nameField = colRef.RefMetadata.Definition.NameField;
            var idField = colRef.RefMetadata.Definition.IdField;
            yield return $"[{col.Name}{sf}.{idField}!{colRef.ModelType}!Id] = r{c.Index}.[{idField}], [{col.Name}{sf}.Name!{colRef.ModelType}!Name] = r{c.Index}.[{nameField}]";
        }
    }

    internal void OnEndInit()
    {
        Definition ??= new();
        if (Definition.HiddenColumns != null)
        {
            foreach (var hc in Definition.HiddenColumns.Split(',')) {
                var fc = Columns.FirstOrDefault(x => x.Name == hc);
                if (fc != null)
                    Columns.Remove(fc);
            }
        }
        var vf = Columns.FirstOrDefault(x => x.Name == Definition.VoidField);
        if (vf != null)
            Columns.Remove(vf);
        var ff = Columns.FirstOrDefault(x => x.Name == Definition.IsFolderField);
        if (ff != null)
            Columns.Remove(ff);
    }
}

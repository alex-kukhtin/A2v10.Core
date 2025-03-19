// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Xaml;
using Newtonsoft.Json;

namespace A2v10.Metadata;

public enum ColumnDataType
{
    Id,
    Reference,
    String,
    // sql
    BigInt,
    Int,
    NVarChar,
    NChar,
    Bit,
    Date,
    DateTime,
    Money,
    Float,
    Uniqueidentifier
}

public record ColumnReference
{
    public String RefSchema { get; init; } = default!;
    public String RefTable { get; init; } = default!;

    /* OLD */
    public String RefColumn { get; init; } = default!;
    public String ModelType => $"TR{RefTable.Singular()}";
    public TableMetadata RefMetadata { get; set; } = new();
    public String EndpointPath { get; set; } = default!;
}
public record TableColumn
{
    #region Database Fields
    public String Name { get; init; } = default!;
    public ColumnDataType DataType { get; init; } = default!;
    public Int32? MaxLength { get; init; }
    public ColumnReference? Reference { get; init; }
    public String? DbName { get; init; }
    public ColumnDataType? DbDataType { get; init; }
    #endregion

    internal Boolean Exists => DbName != null && DbDataType != null;
    internal Boolean IsReference => Reference != null;    
    internal Boolean IsParent => Name == "Parent";
    internal Boolean IsSearchable => DataType == ColumnDataType.String;
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

    
    internal String VoidField => Void ?? "Void";
    internal String NameField => Name ?? "Name";
    internal String IdField => Id ?? "Id";
    internal String IsFolderField => IsFolder ?? "IsFolder";

    internal static TableDefinition Merge(TableDefinition? source, TableDefinition? other)
    {
        if (source != null && other != null)
        {
            var hiddenSource = source.HiddenColumns?.Split(',') ?? [];
            var hiddenOther = other.HiddenColumns?.Split(',') ?? [];
            return new TableDefinition()
            {
                Void = source.Void ?? other.Void,
                Name = source.Name ?? other.Name,
                Id = source.Id ?? other.Id,
                IsFolder = source.IsFolder ?? other.IsFolder,
                HiddenColumns = String.Join(',', hiddenSource.Union(hiddenOther))
            };
        }
        else if (source == null && other != null)
            return other;
        else if (source != null && other == null)
            return source;
        return new TableDefinition();
    }
}

public record FormColumn
{
    public String Path { get; init; } = default!;
    public String? Header { get; set; }
    public Boolean NoSort { get; init; }
    public Boolean Filter { get; init; }
    public Int32 Width { get; init; }
    public Int32 Clamp { get; init; }    

    // internal 
    internal ColumnRole Role { get; init; } 
    internal String? SortProperty { get; init; }    
}
public record Form
{    
    public Int32 Width { get; init; }
    public String? Title { get; init; }
    public List<FormColumn> Columns { get; set; } = [];
}

public record TableMetadata
{
    #region Database fields
    public String Schema { get; init; } = default!;
    public String Name { get; init; } = default!;
    public List<TableColumn> Columns { get; private set; } = [];
    public String? ItemsName { get; init; }
    public String? ItemName { get; init; }
    public String? TypeName { get; init; }
    #endregion

    // OLD
    public TableDefinition Definition { get; set; } = default!;
    internal String SqlTableName => $"{Schema}.[{Name}]";
    internal String ModelType => $"T{Name.Singular()}";

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

    internal TableMetadata MergeGlobal(TableMetadata global)
    {
        var def = TableDefinition.Merge(Definition, global.Definition);

        var hiddenColumns = def.HiddenColumns?.Split(',').ToHashSet() ?? [];

        Boolean IsFieldVisible(TableColumn col) =>
            !hiddenColumns.Contains(col.Name) && col.Name != def.VoidField && col.Name != def.IsFolderField;

        return new TableMetadata()
        {
            Definition = def,
            Name = this.Name,
            Schema = this.Schema,
            Columns = Columns.Where(IsFieldVisible).ToList()
        };
    }
}

public record AppMetadata
{
    public ColumnDataType IdDataType { get; init; }
    public TableMetadata[] Tables { get; init; } = [];

    // field names
    public String? Id { get; init; }
    public String? Name { get; init; }
    public String? Void { get; init; }
    public String? IsSystem { get; init; }
    public String? IsFolder { get; init; }

    internal String IdField => Id ?? nameof(Id);
    internal String NameField => Name ?? nameof(Name);
    internal String VoidField => Name ?? nameof(Void);
    internal String IsFolderField => Name ?? nameof(IsFolder);
    internal String IsSystemField => Name ?? nameof(IsSystem);
    internal Boolean HasConstraint(String name) => name == IsSystemField || name == IsFolderField || name == VoidField;
    internal static AppMetadata FromDataModel(IDataModel model)
    {
        var json = JsonConvert.SerializeObject(model.Root.Get<Object>("Application"))
            ?? throw new InvalidOperationException("Application is null");
        return JsonConvert.DeserializeObject<AppMetadata>(json)
            ?? throw new InvalidOperationException("AppMetadata deserialization fails");
    }
}
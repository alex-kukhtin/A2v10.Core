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
    public String ModelType => $"TR{RefTable.Singular()}";
    public String EndpointPath { get; set; } = default!;
}
public record TableColumn
{
    #region Database Fields
    public String Name { get; init; } = default!;
    public ColumnDataType DataType { get; init; } = default!;
    public Int32? MaxLength { get; init; }
    public ColumnReference Reference { get; init; } = default!;
    public String? DbName { get; init; }
    public ColumnDataType? DbDataType { get; init; }
    #endregion
    internal Boolean IsReference => Reference != null && Reference.RefTable != null;
    internal Boolean Exists => DbName != null && DbDataType != null;

    // Old
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
    internal DataType BindDataType {  get; init; }  
    internal String? SortProperty { get; init; }    
}
public record FormOld
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

    // TODO
    internal String ModelType => $"T{Name.Singular()}";
    internal String RealItemName => ItemName ?? Name.Singular();
    internal String RealItemsName => ItemsName ?? Name;  

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
        return JsonConvert.DeserializeObject<AppMetadata>(json, JsonSettings.IgnoreNull)
            ?? throw new InvalidOperationException("AppMetadata deserialization fails");
    }
}
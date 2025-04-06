// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

public enum ColumnDataType
{
    Id,
    Reference,
    String,
    Stream,
    Enum,
    Operation,
    // sql
    BigInt,
    Int,
    SmallInt,
    NVarChar,
    NChar,
    Bit,
    Date,
    DateTime,
    Money,
    Float,
    Uniqueidentifier,
    VarBinary,
}

public record ColumnReference
{
    public String RefSchema { get; init; } = default!;
    public String RefTable { get; init; } = default!;
    internal String SqlTableName => $"{RefSchema}.[{RefTable}]";
}

public record TableColumn
{
    #region Database Fields
    public String Name { get; init; } = default!;
    public ColumnDataType DataType { get; init; } = default!;
    public Int32 MaxLength { get; init; }
    public ColumnReference Reference { get; init; } = default!;
    public String? DbName { get; init; }
    public ColumnDataType? DbDataType { get; init; }
    public Boolean IsPK { get; init; }  
    #endregion
    internal Boolean IsReference => Reference != null && Reference.RefTable != null;
    internal Boolean Exists => DbName != null && DbDataType != null;

    // Old
    internal Boolean IsParent => Name == "Parent";
    internal Boolean IsSearchable => DataType == ColumnDataType.String;
}


public enum EditWithMode
{
    Dialog,
    Page 
}

public enum ApplySourceKind
{
    Table,
    Details
}
public record ApplyMapping
{
    public String Target { get; init; } = default!;

    public String Source { get; init; } = default!;

    public ApplySourceKind Kind {get; init;}
}

public record TableApply
{
    #region Database Fields
    public Int16 InOut { get; init; } = default!;
    public Boolean Storno { get; init; }
    public ColumnReference Journal { get; init; } = default!;
    public ColumnReference? Details { get; init; }
    public List<ApplyMapping>? Mapping { get; init; } = [];
    #endregion
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
    public EditWithMode EditWith { get; init; }
    public List<TableMetadata> Details { get; private set; } = [];
    public ColumnReference? ParentTable { get; init; }
    #endregion
    public List<TableApply>? Apply { get; init; }

    // internal variables
    internal String RealItemName => ItemName ?? Name.Singular();
    internal String RealItemsName => ItemsName ?? Name;  
    internal String RealTypeName => $"T{TypeName ?? RealItemName}";
    internal String TableTypeName => $"{Schema}.[{Name}.TableType]";
    internal String SqlTableName => $"{Schema}.[{Name}]";
    internal Boolean IsDocument => Schema == "doc";
    internal Boolean IsJournal => Schema == "jrn";
    internal Boolean IsOperation => Schema == "op";

    internal IEnumerable<TableColumn> PrimaryKeys => Columns.Where(c => c.IsPK);
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
    public String? RowNo { get; init; }

    // internal
    internal String IdField => Id ?? nameof(Id);
    internal String NameField => Name ?? nameof(Name);
    internal String VoidField => Void ?? nameof(Void);
    internal String IsFolderField => IsFolder ?? nameof(IsFolder);
    internal String IsSystemField => IsSystem ?? nameof(IsSystem);
    internal String RowNoField => RowNo ?? nameof(RowNo);

    internal String ParentField = "Parent";
    internal Boolean HasDefault(String name) => name == IsSystemField || name == IsFolderField || name == VoidField;

    internal static AppMetadata FromDataModel(IDataModel model)
    {
        var json = JsonConvert.SerializeObject(model.Root.Get<Object>("Application"))
            ?? throw new InvalidOperationException("Application is null");
        return JsonConvert.DeserializeObject<AppMetadata>(json, JsonSettings.IgnoreNull)
            ?? throw new InvalidOperationException("AppMetadata deserialization fails");
    }
}
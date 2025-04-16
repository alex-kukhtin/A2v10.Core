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

public record ReferenceMember(TableColumn Column, TableMetadata Table, Int32 Index);

public record ColumnReference
{
    public String RefSchema { get; init; } = default!;
    public String RefTable { get; init; } = default!;
    internal String SqlTableName => $"{RefSchema}.[{RefTable}]";
}

[Flags]
public enum TableColumnRole
{
    PrimaryKey = 0x1,  // 1
    Name       = 0x2,  // 2
    Code       = 0x4,  // 4
    RowNo      = 0x8,  // 8 (RowNo + PrimaryKey) = 0x9 (9)
    Void       = 0x10, // 16
    Parent     = 0x20, // 32 (Parent + PrimaryKey) = 0x33
    IsFolder   = 0x40, // 64
    IsSystem   = 0x80, // 128
    Done       = 0x100, // 256
    Kind       = 0x200, // 512
    Owner      = 0x400, // 1024
    Number     = 0x800  // 2048
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
    public TableColumnRole Role { get; init; } = default!;
    public Int32 Order { get; init; }
    #endregion
    internal Boolean IsReference => Reference != null && Reference.RefTable != null;
    internal Boolean Exists => DbName != null && DbDataType != null;

    internal Boolean HasDefault => 
           Role.HasFlag(TableColumnRole.IsFolder)
        || Role.HasFlag(TableColumnRole.IsSystem)
        || Role.HasFlag(TableColumnRole.Void)
        || Role.HasFlag(TableColumnRole.Done);

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
    public String? DetailsKind { get; init; }
    #endregion
}

public record DetailsKind(String Name, String Label);
public record TableMetadata
{
    #region Database fields
    public String Schema { get; init; } = default!;
    public String Name { get; init; } = default!;
    public List<TableColumn> Columns { get; internal set; } = [];
    public String? ItemsName { get; init; }
    public String? ItemName { get; init; }
    public String? TypeName { get; init; }
    public EditWithMode EditWith { get; init; }
    public List<TableMetadata> Details { get; private set; } = [];
    public ColumnReference? ParentTable { get; init; }

    public String? ItemsLabel { get; init; }
    public String? ItemLabel { get; init; }
    #endregion
    public List<TableApply>? Apply { get; init; }
    public List<DetailsKind> Kinds { get; init; } = [];

    // internal variables
    internal String PrimaryKeyField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.PrimaryKey) && !c.Role.HasFlag(TableColumnRole.RowNo))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Primary Key");
    internal String VoidField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.Void))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Void column");
    internal String RowNoField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.RowNo))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a RowNumber column");
    internal String NameField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.Name))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Name column");
    internal String DoneField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.Done))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Done column");
    internal String KindField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.Kind))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Kind column");

    internal String RealItemName => ItemName ?? Name.Singular();
    internal String RealItemsName => ItemsName ?? Name;  
    internal String RealTypeName => $"T{TypeName ?? RealItemName}";
    internal String TableTypeName => $"{Schema}.[{Name}.TableType]";
    internal String RealItemLabel => ItemLabel ?? $"@{RealItemName}";
    internal String RealItemsLabel => ItemsLabel ?? $"@{RealItemsName}";
    internal String SqlTableName => $"{Schema}.[{Name}]";
    internal Boolean IsDocument => Schema == "doc";
    internal Boolean IsJournal => Schema == "jrn";
    internal Boolean IsOperation => Schema == "op";

    internal IEnumerable<TableColumn> PrimaryKeys => Columns.Where(c => c.Role.HasFlag(TableColumnRole.PrimaryKey));
}

public record OperationMetadata(String Id, String? Name);

public record AppMetadata
{
    public ColumnDataType IdDataType { get; init; }
    public TableMetadata[] Tables { get; init; } = [];
    public OperationMetadata[] Operations { get; init; } = []; 

    // internal
    internal static AppMetadata FromDataModel(IDataModel model)
    {
        var json = JsonConvert.SerializeObject(model.Root.Get<Object>("Application"))
            ?? throw new InvalidOperationException("Application is null");
        return JsonConvert.DeserializeObject<AppMetadata>(json, JsonSettings.IgnoreNull)
            ?? throw new InvalidOperationException("AppMetadata deserialization fails");
    }
}
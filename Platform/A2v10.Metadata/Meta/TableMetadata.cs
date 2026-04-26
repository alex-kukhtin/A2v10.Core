// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

public enum EndpointKind
{
    Undefined,
    Catalog,
    Document,
    Journal,
}
public enum ColumnType
{
    // semantic types
    Id,
    String,
    Ref,
    Date,
    DateTime,
    Money,
    Boolean,
    //
    Stream,
    Enum,
    Operation,
    // sql
    BigInt,
    Int,
    SmallInt,
    Decimal,
    NVarChar,
    NChar,
    Bit,
    Float,
    Uniqueidentifier,
    VarBinary,
    RowVersion,
    TimeStamp = RowVersion  // SQL INFORMATION_SCHEMA.DATA_TYPE uses TimeStamp
}

public record ReferenceMember(TableColumn Column, TableMetadata Table, Int32 Index);

public record ColumnReference
{
    public String RefSchema { get; init; } = default!;
    public String RefTable { get; init; } = default!;
    internal String SqlTableName => $"{RefSchema}.[{RefTable}]";
}

public record ColumnReferenceToMe : ColumnReference
{
    public String Column { get; init; } = default!;
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
    Number     = 0x800, // 2048
    SystemName = 0x1000,// 4096
}

public record TableColumn
{
    public String Name { get; set; } = default!;
    public ColumnType Type { get; init; } = default!;
    public String Target { get; init; } = default!;

    #region Database Fields
    public String? Label { get; init; } = default!;
    public Int32 MaxLength { get; init; }
    public Int32 Scale { get; init; }
    public ColumnReference Reference { get; init; } = default!;
    public String? DbName { get; init; }
    public ColumnType? DbDataType { get; init; }
    public TableColumnRole Role { get; init; } = default!;
    public Int32 Order { get; init; }
    public Int32 DbOrder { get; init; }
    public String? Computed { get; init; }
    public Boolean Required { get; init; }
    public Boolean Total { get; init; }
    public Boolean Unique { get; init; }
    #endregion
    internal Boolean IsReference => Reference != null && Reference.RefTable != null && Type != ColumnType.Enum;
    internal Boolean IsEnum => Type == ColumnType.Enum;
    internal Boolean IsBitField => Type == ColumnType.Bit && Role == 0;
    internal Boolean IsBlob => Type == ColumnType.Stream;
    internal Boolean IsString => Type == ColumnType.String;
    internal Boolean Exists => DbName != null && DbDataType != null;

    internal Boolean HasDefaultBit => 
           Role.HasFlag(TableColumnRole.IsFolder)
        || Role.HasFlag(TableColumnRole.IsSystem)
        || Role.HasFlag(TableColumnRole.Void)
        || Role.HasFlag(TableColumnRole.Done);

    internal Boolean IsVoid => Role.HasFlag(TableColumnRole.Void);
    internal Boolean IsParent => Role.HasFlag(TableColumnRole.Parent);
    internal Boolean IsName => Role.HasFlag(TableColumnRole.Name);
    internal Boolean IsRowNo => Role.HasFlag(TableColumnRole.RowNo);
    internal Boolean IsSearchable => Type == ColumnType.String;
    internal Boolean IsMemo => Name == "Memo";
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

public enum ReportItemKind
{
    G,
    F,
    D,
    Grouping = G,
    Filter = F,
    Data = D
}
public record ReportItemMetadata
{
    #region Database Fields 
    public ReportItemKind Kind { get; init; }
    public String Column { get; init; } = default!;
    public ColumnType DataType { get; init; } = default!;
    public String RefSchema { get; init; } = default!;
    public String RefTable { get; init; } = default!;
    public Boolean Checked { get; init; }
    public Int32 Order { get; init; }
    public String? Label { get; init; }
    public String? Func { get; init; }
    #endregion

    public String RealRefSchema => DataType switch
    {
        ColumnType.Operation => "op",
        _ => RefSchema
    };
    public String RealRefTable => DataType switch
    {
        ColumnType.Operation => "operations", // Lower case is important!
        _ => RefTable
    };
}
public record DetailsKind(String Name, String Label);
public record TableMetadata
{
    #region Database fields
    public EndpointKind Kind { get; set; }
    public String Schema { get; set; } = default!;
    public String Table { get; set; } = default!;
    public String Model { get; set; } = default!;
    public String Path { get; set; } = default!;
    public Dictionary<String, TableColumn> Fields { get; init; } = [];

    public List<TableColumn> Columns => [.. Fields.Select(
        kp => { kp.Value.Name = kp.Key; return kp.Value; }
    )];

    // for sql
    public String? TypeName => $"T{Model}";
    public String CollectionName => Model.Plural();


    // OLD
    public String Name { get; init; } = default!;

    //public List<TableColumn> Columns { get; internal set; } = [];
    public String? ItemsName { get; init; }
    public String? ItemName { get; init; }
    public EditWithMode EditWith { get; init; }
    public List<TableMetadata> Details { get; private set; } = [];
    public ColumnReference? ParentTable { get; init; }

    public String? ItemsLabel { get; init; }
    public String? ItemLabel { get; init; }
    public String? Type { get; init; }
    public Boolean UseFolders { get; init; }    
    public String? DbName { get; init; }
    public String? DbSchema { get; init; }
    #endregion
    public List<TableApply>? Apply { get; init; }
    public List<DetailsKind> Kinds { get; init; } = [];
    public List<ReportItemMetadata> ReportItems { get; init; } = [];
    public List<ColumnReferenceToMe> RefsToMe { get; init; } = [];
    // internal variables
    internal String PrimaryKeyField => "Id";
    internal String IsFolderField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.IsFolder))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a IsFolder column");
    internal String ParentField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.Parent))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Parent column");
    internal String RowNoField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.RowNo))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a RowNumber column");
    internal String NameField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.Name))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Name column");
    internal String DoneField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.Done))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Done column");
    internal String KindField => Columns.FirstOrDefault(c => c.Role.HasFlag(TableColumnRole.Kind))?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Kind column");

    internal String RealItemName => ItemsName != null ? ItemsName.Singular() : ItemName ?? Name.Singular();
    internal String RealItemsName => ItemsName ?? Name;  
    internal String RealTypeName => $"T{Name}";
    internal String TableTypeName => $"{Schema}.[{Model}.Meta.TableType]";
    internal String RealItemLabel => ItemLabel ?? $"@{RealItemName}";
    internal String RealItemsLabel => ItemsLabel ?? $"@{RealItemsName}";
    internal String SqlTableName => $"{Schema}.[{Table}]";
    internal Boolean IsDocument => Schema == "doc";
    internal Boolean IsEnum => Schema == "enm";
    internal Boolean IsJournal => Schema == "jrn";
    internal Boolean IsOperation => Schema == "op";
    internal Boolean HasDbTable => !String.IsNullOrEmpty(DbName) && !String.IsNullOrEmpty(DbSchema);

    internal IEnumerable<TableColumn> PrimaryKeys => Columns.Where(c => c.Role.HasFlag(TableColumnRole.PrimaryKey));
    internal Boolean HasSequence => PrimaryKeys.Count() == 1 && PrimaryKeys.First().Type == ColumnType.Id;

    internal void SetDefaults(String schema, String table)
    {
        Path = $"{schema}/{table}";
        if (String.IsNullOrEmpty(Schema))
            Schema = schema.FromFolder();
        if (String.IsNullOrEmpty(Table))
            Table = table.Plural();
        if (String.IsNullOrEmpty(Model))
            Model = table.ToPascalCase();
        if (Kind == EndpointKind.Undefined)
            Kind = schema.ToEndpointKind();
    }
}

public record OperationMetadata(String Id, String? Name, String? Category);

public record EnumValueMetadata(String Id, String Name, Int32 Order, Boolean? Inactive);
public record EnumMetadata(String Name, EnumValueMetadata[] Values);

public record AppMetadata
{
    public Guid Id { get; init; } = default!;
    public ColumnType IdDataType { get; init; }
    public TableMetadata[] Tables { get; init; } = [];
    public OperationMetadata[] Operations { get; init; } = [];
    public EnumMetadata[] Enums { get; init; } = [];
    public String Title { get; init; } = default!;
    // internal
    internal static AppMetadata FromDataModel(IDataModel model)
    {
        var json = JsonConvert.SerializeObject(model.Root.Get<Object>("Application"))
            ?? throw new InvalidOperationException("Application is null");
        return JsonConvert.DeserializeObject<AppMetadata>(json, JsonSettings.IgnoreNull)
            ?? throw new InvalidOperationException("AppMetadata deserialization fails");
    }
}
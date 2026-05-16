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
    Operation,
    Journal,
    Details
}
public enum ColumnType
{
    // semantic types
    Id,
    Name,
    Memo,
    RowNumber,
    Done,
    Void,
    IsSystem,
    IsFolder,
    Owner,
    Parent,
    User,
    RowKind,
    Operation,
    Document,
    RowVersion,
    // Simple fields
    String,
    Ref,
    Date,
    DateTime,
    Money,
    Boolean,
    //
    Stream,
    Enum,
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
    TimeStamp = RowVersion  // SQL INFORMATION_SCHEMA.DATA_TYPE uses TimeStamp
}

public record ReferenceMember(TableColumn Column, TableMetadata Table, Int32 Index);
public record RefDescriptor(Int32 Index, TableColumn Column, TableMetadata Table);

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

public record TableColumn
{
    public TableColumn() { }
    public TableColumn(String name, ColumnType type)
    {
        Name = name;
        Type = type;
    }
    public String Name { get; set; } = default!;
    public ColumnType Type { get; init; } = default!;
    public String Target { get; init; } = default!;
    public TableMetadata? RefTable { get; set; }
    public TableMetadata RefTableCheck => RefTable ?? throw new InvalidOperationException($"RefTable for '{Name}' is null");

    // for metadata provider
    internal Boolean IsRef => Type == ColumnType.Ref || Type == ColumnType.Owner || 
            Type == ColumnType.User || Type == ColumnType.Document || Type == ColumnType.Operation;

    internal String Presentation
    {
        get
        {
            if (Type == ColumnType.Ref)
                return RefTableCheck.Label;
            return Constants.FieldNames.Name;
        }
    }

    #region Database Fields
    public Int32 MaxLength { get; init; }
    public Int32 Scale { get; init; }
    public ColumnReference Reference { get; init; } = default!;
    public String? DbName { get; init; }
    public ColumnType? DbDataType { get; init; }
    
    public String? Computed { get; init; }
    public Boolean Required { get; init; }
    public Boolean Total { get; init; }
    public Boolean Unique { get; init; }
    #endregion

    internal Boolean IsReference => Reference != null && Reference.RefTable != null && Type != ColumnType.Enum;
    internal Boolean IsEnum => Type == ColumnType.Enum;

    internal Boolean HasDefaultBit => 
           Type == ColumnType.IsFolder
        || Type == ColumnType.IsSystem
        || Type == ColumnType.Void
        || Type == ColumnType.Done;

    internal Boolean IsVoid => Type == ColumnType.Void;
    internal Boolean IsSearchable => Type == ColumnType.String || Type == ColumnType.Name || Type == ColumnType.Memo;
    internal Boolean IsMemo => Type == ColumnType.Memo;
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
public enum TableTrait
{
    Audit,
    Tags,
    Color
}

public record TableMetadata
{
    #region Database fields
    public EndpointKind Kind { get; set; }
    public String Schema { get; set; } = default!;
    public String Table { get; set; } = default!;
    public String Model { get; set; } = default!;
    public String Path { get; set; } = default!;
    public String Label { get; set; } = default!;

    public Dictionary<String, TableColumn> Fields { get; init; } = [];

    [JsonIgnore]
    public List<TableColumn> Columns => [.. Fields.Select(
        kp => { kp.Value.Name = kp.Key; return kp.Value; }
    )];

    public List<TableTrait> Traits { get; init; } = [];

    public Dictionary<String, FormMetadata> Forms { get; init; } = [];
    public String? Storage { get; set; }
    public Dictionary<String, TableMetadata> Details { get; private set; } = [];
    public List<String> Kinds { get; init; } = [];

    [JsonIgnore]
    internal TableMetadata? Origin { get; set; }

    [JsonIgnore]
    internal String TableTypeName => $"{Schema}.[{Model}.Meta.TableType]";

    // for sql
    [JsonIgnore]
    public String TypeName => $"T{Model}";
    [JsonIgnore]
    public String RefTypeName => $"TR{Model}";
    [JsonIgnore]
    public String CollectionName => Model.Plural();
    // OLD
    public String? ItemsName { get; init; }
    public String? ItemName { get; init; }
    public EditWithMode EditWith { get; init; }

    #endregion


    public String? ItemsLabel { get; init; }
    public String? ItemLabel { get; init; }
    public String? Type { get; init; }
    public Boolean UseFolders { get; init; }    
    public String? DbName { get; init; }
    public String? DbSchema { get; init; }
    public List<TableApply>? Apply { get; init; }
    public List<ReportItemMetadata> ReportItems { get; init; } = [];
    public List<ColumnReferenceToMe> RefsToMe { get; init; } = [];

    // internal variables
    public String SqlTableName => $"{Schema}.[{Table}]";

    internal String PrimaryKeyField => "Id";
    internal String ParentField => Columns.FirstOrDefault(c => c.Type == ColumnType.Parent)?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Parent column");
    internal String NameField => Columns.FirstOrDefault(c => c.Type == ColumnType.Name)?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Name column");
    internal String DoneField => Columns.FirstOrDefault(c => c.Type == ColumnType.Done)?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a Done column");
    internal String RowKindField => Columns.FirstOrDefault(c => c.Type == ColumnType.RowKind)?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a RowKind column");

    internal String RealItemName => ItemsName != null ? ItemsName.Singular() : ItemName ?? Table.Singular();
    internal String RealItemsName => ItemsName ?? Table;  
    internal String RealItemsLabel => ItemsLabel ?? $"@{RealItemsName}";
    internal Boolean IsJournal => Schema == "jrn";
    internal Boolean IsOperation => Schema == "op";
    internal Boolean IsDocument => Schema == "doc";
    internal Boolean HasDbTable => !String.IsNullOrEmpty(DbName) && !String.IsNullOrEmpty(DbSchema);
    internal Boolean HasPeriod => IsDocument || IsJournal;

    internal IEnumerable<TableColumn> PrimaryKeys => Columns.Where(c => c.Type == ColumnType.Id);
    internal Boolean HasSequence => PrimaryKeys.Count() == 1 && PrimaryKeys.First().Type == ColumnType.Id;

    internal void SetDetailDefaults(TableMetadata table)
    {
        Schema = table.Schema;
        Kind = EndpointKind.Details;
        if (String.IsNullOrEmpty(Table))
            Table = Model.ToPascalCase().Plural();
    }
    internal void SetDefaults(String schema, String table)
    {
        Path = $"/{schema}/{table}";
        if (String.IsNullOrEmpty(Schema))
            Schema = schema.FromFolder();
        if (String.IsNullOrEmpty(Table))
            Table = table.ToPascalCase().Plural();
        if (String.IsNullOrEmpty(Model))
            Model = table.ToPascalCase();
        if (Kind == EndpointKind.Undefined)
            Kind = schema.ToEndpointKind();
        if (String.IsNullOrEmpty(Label))
            Label = Constants.FieldNames.Name;

        foreach (var d in Details)
            d.Value.SetDetailDefaults(this);

        if (!Forms.ContainsKey(Constants.FormNames.Index))
            Forms.Add(Constants.FormNames.Index, DefaultFormBuilder.CreateIndexForm(this));
        if (!Forms.ContainsKey(Constants.FormNames.Edit))
            Forms.Add(Constants.FormNames.Edit, DefaultFormBuilder.CreateEditForm(this));
        Forms[Constants.FormNames.Index].SetDefaults(this);
        Forms[Constants.FormNames.Edit].SetDefaults(this);
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
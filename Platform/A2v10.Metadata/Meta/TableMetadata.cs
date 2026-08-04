// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;

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
    Details,
    Folders,
    Tags,
    TagEntries
}
public enum ColumnType
{
    // semantic types
    String, // DEFAULT VALUE!!!
    Id,
    Name,
    Memo,
    RowNumber,
    Done,
    Void,
    IsSystem,
    Owner,
    Parent,
    Folder,
    User,
    RowKind,
    Operation,
    Document,
    DocumentType,  // polymorphic Document reference: which storage the id lives in (see SqlBuilderPost)
    Row,        // journal provenance: Id of the posted detail row (r.[Id]); null when the post has no 'each'
    RowVersion,
    Color,
    Enum,
    Autonum,
    Company,
    Direction,  // journal leg sign (+1/-1); vocabulary (In/Out, Dt/Ct) is presentation
    // Semantic Values
    Amount,
    Price,
    Qty,
    Percent,
    Factor,
    // Simple fields
    Ref,
    Date,
    DateTime,
    Money,
    Boolean,
    //
    Stream,
    // neutral tier - values without business semantics, and the only place an author
    // may refuse behaviour. Raw SQL spellings are gone: one integer, one number, no
    // floating point, no fixed-length strings.
    Integer,
    Number,
    // raw
    BigInt,
    Bit,
    NChar,
    Decimal,
    Float,
    VarBinary,
    Uniqueidentifier
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
    public String? Target { get; init; } // for refs

    /* A reference points at an ENDPOINT that must resolve to one of our tables: the address
     * comes from the endpoint, the type and column names from its table. Neither is derivable
     * from the other, which is why the resolved link is the container and not the table.
     */
    [JsonIgnore]
    public NormalEndpointMetadata? RefTable { get; set; }
    [JsonIgnore]
    public NormalEndpointMetadata RefTableCheck => RefTable ?? throw new InvalidOperationException($"RefTable for '{Name}' is null");

    [JsonIgnore] 
    internal Boolean IsRef => Type == ColumnType.Ref || Type == ColumnType.Owner || 
            Type == ColumnType.User || Type == ColumnType.Document || 
            Type == ColumnType.Company || Type == ColumnType.Operation;

    internal String Presentation
    {
        get
        {
            if (Type == ColumnType.Ref)
                return RefTableCheck.Storage.Label;
            return Constants.FieldNames.Name;
        }
    }

    #region Database Fields
    public Int32? Length { get; init; }
    public Int32? Precision { get; init; }
    public Int32? Scale { get; init; }    
    // OLD -> to RULES
    public Boolean Required { get; init; }
    public Boolean Total { get; init; }
    public Boolean Unique { get; init; }
    #endregion
    internal Boolean IsEnum => Type == ColumnType.Enum;
    internal Boolean IsOperation => Type == ColumnType.Operation;

    [JsonIgnore]
    internal Boolean HasDefaultBit => 
        Type == ColumnType.IsSystem
        || Type == ColumnType.Void
        || Type == ColumnType.Done;

    [JsonIgnore]
    internal Boolean IsVoid => Type == ColumnType.Void;
    [JsonIgnore]
    internal Boolean IsSearchable => Type == ColumnType.String || Type == ColumnType.Name || Type == ColumnType.Memo;
    [JsonIgnore]
    internal Boolean IsMemo => Type == ColumnType.Memo;
    [JsonIgnore]
    internal String Header => $"@[{Name}]";
    internal String DisplayPath => (IsRef) ? $"{Name}.{Presentation}" : Name;
}

public enum PostDirection
{
    None,
    In,
    Out,
    Debit = In,
    Credit = Out
}

public sealed record PostMetadata
{
    #region JSON Fields
    public String Journal { get; init; } = default!;
    public PostDirection Dir { get; init; }
    public Boolean Storno { get; init; }
    public List<String> Each { get; init; } = [];
    public Dictionary<String, String> Document { get; init; } = [];
    public Dictionary<String, String> Row { get; init; } = [];
    #endregion

    [JsonIgnore]
    public TableMetadata? JournalTable { get; set; }
    [JsonIgnore]
    public TableMetadata JournalTableCheck => JournalTable ?? throw new InvalidOperationException($"RefTable for '{Journal}' is null");
    [JsonIgnore]
    public Int16 InOutInt => Dir switch { PostDirection.In => 1, PostDirection.Out => -1, _ => 0 };
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

public enum InitialSource
{
    Literal,
    Context,
    Profile,
    Policy,
    Sql
}

public enum TableTrait
{
    Audit,
    Hierarchy,
    Folders,
    Tags
}

public sealed record TableMetadata
{
    #region Database fields
    public EndpointKind Kind { get; set; }
    public String Schema { get; set; } = default!;
    public String Table { get; set; } = default!;
    public String Model { get; set; } = default!;
    public String Path { get; set; } = default!;
    public String Label { get; set; } = default!;
    /* Declarations that stay here: 'inherit' is genuinely two-layer - the storage carries the
     * base, the endpoint overrides it in DeclarationMetadata. A key belongs in both types only
     * under that test.
     */
    public Dictionary<String, InheritMetadata> Inherit { get; init; } = [];

    [JsonProperty("fields")]
    private Dictionary<String, TableColumn> _fields { get; init; } = [];

    [JsonIgnore]
    public List<TableColumn> Columns => [.. _fields.Select(
        kp => { kp.Value.Name = kp.Key; return kp.Value; }
    )];
    public Dictionary<String, TableMetadata> Details { get; private set; } = [];
    public List<String> Kinds { get; init; } = [];
    public List<TableTrait> Traits { get; init; } = [];

    public ConcurrentDictionary<String, FormMetadata> Forms { get; init; } = [];

    // for sql
    [JsonIgnore]
    public String TypeName => $"T{Model}";
    [JsonIgnore]
    public String RefTypeName => $"TR{Model}";
    [JsonIgnore]
    public String CollectionName => Model.Plural();

    [JsonIgnore]
    public Boolean EditWithPage => IsDocument;


    [JsonIgnore]
    public Boolean HasTags => Traits.Contains(TableTrait.Tags);
    public Boolean HasFolders => Traits.Contains(TableTrait.Folders);

    // OLD
    public String? ItemsName { get; init; }

    #endregion


    public String? ItemsLabel { get; init; }
    public List<ColumnReferenceToMe> RefsToMe { get; init; } = [];

    // Service variables
    [JsonIgnore]
    public String SqlSchema => Schema.ToSqlSchema();
    [JsonIgnore]
    public String SqlTableName => $"{SqlSchema}.[{Table}]";
    [JsonIgnore]
    public String SqlSequenceName => $"{SqlSchema}.[SQ_{Table}]";
    [JsonIgnore]
    internal String SqlTableTypeName => $"{SqlSchema}.[{Model}.Meta.TableType]";
    [JsonIgnore]
    public String? FileHash { get; set; }

    internal String RowKindField => Columns.FirstOrDefault(c => c.Type == ColumnType.RowKind)?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a RowKind column");

    internal String RealItemsName => ItemsName ?? Table;  
    internal String RealItemsLabel => ItemsLabel ?? $"@{RealItemsName}";

    [JsonIgnore]
    internal Boolean IsCatalog => Kind == EndpointKind.Catalog;
    [JsonIgnore]
    internal Boolean IsDocument => Kind == EndpointKind.Document;
    [JsonIgnore]
    internal Boolean IsJournal => Kind == EndpointKind.Journal;
    [JsonIgnore]
    internal Boolean IsTags => Kind == EndpointKind.Tags;
    internal Boolean IsTagEntries => Kind == EndpointKind.TagEntries;
    [JsonIgnore]
    internal Boolean HasPeriod => IsDocument || IsJournal;

    internal void SetDetailDefaults(TableMetadata table)
    {
        Schema = table.Schema;
        Kind = EndpointKind.Details;
        if (String.IsNullOrEmpty(Table))
            Table = Model.ToPascalCase().Plural();
    }
    internal void SetDefaults(String schema, String table)
    {
        // the file that declares this table; spelled like EndpointMetadata.Path, because a
        // DocumentType discriminator is this value and has to be comparable to an address
        Path = String.IsNullOrEmpty(table) ? $"/{schema}" : $"/{schema}/{table}";
        if (String.IsNullOrEmpty(Schema))
            Schema = schema;
        /* No default for Table. Pluralising the folder name looks like a convention but is a
         * guess: English plurals are irregular, and the name has to be reproduced exactly
         * wherever it surfaces later - migrations, deploy, ejected SQL, legacy mapping - where
         * a near miss creates a second table instead of failing. DatabaseMetadataProvider
         * requires it to be declared, so an empty Table here belongs to a kind that has none.
         */
        if (String.IsNullOrEmpty(Model))
            Model = table.ToPascalCase();
        if (Kind == EndpointKind.Undefined)
            Kind = schema.ToEndpointKind();
        if (String.IsNullOrEmpty(Label))
            Label = Constants.FieldNames.Name;

        foreach (var d in Details)
            d.Value.SetDetailDefaults(this);
    }
}
public record OperationMetadata(String Id, String? Name, String? Category);
public record EnumValueMetadata(String Id, String Name, Int32 Order, Boolean? Inactive);
public record EnumMetadata(String Name, EnumValueMetadata[] Values);

public record AppMetadata
{
    public Guid Id { get; init; } = default!;
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
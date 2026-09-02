// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

public enum EndpointKind
{
    Undefined,
    Catalog,
    Document,
    Operation,
    Journal,
    Report,
    Details,
    Folders,
    Tags,
    TagEntries,
    Enum,
    Autonum,
    AutonumValues
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
    IsFolder,
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

public record RefDescriptor(Int32 Index, TableColumn Column, TableMetadata Table);

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
     *
     * IRefTarget and not NormalEndpointMetadata, because a shape can also be served by an
     * endpoint the platform implements itself - see CLAUDE.md, "System endpoints". Every reader
     * asks for exactly the two things the interface carries, so widening it moved no call site.
     */
    [JsonIgnore]
    public IRefTarget? RefTable { get; set; }
    [JsonIgnore]
    public IRefTarget RefTableCheck => RefTable ?? throw new InvalidOperationException($"RefTable for '{Name}' is null");

    [JsonIgnore] 
    internal Boolean IsRef => Type == ColumnType.Ref || Type == ColumnType.Owner ||
            Type == ColumnType.User || Type == ColumnType.Document ||
            Type == ColumnType.Company || Type == ColumnType.Operation ||
            Type == ColumnType.Enum;

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
    /* No 'Required' here: it is a rule, not a property of the column - see
     * DeclarationMetadata.RuleSet and MetadataExtensions.RequiredFields.
     */
    // OLD -> to RULES
    public Boolean Unique { get; init; }
    #endregion
    internal Boolean IsEnum => Type == ColumnType.Enum;
    internal Boolean IsOperation => Type == ColumnType.Operation;

    [JsonIgnore]
    internal Boolean HasDefaultBit => 
        Type == ColumnType.IsSystem
        || Type == ColumnType.IsFolder
        || Type == ColumnType.Void
        || Type == ColumnType.Done;

    [JsonIgnore]
    internal Boolean IsVoid => Type == ColumnType.Void;
    [JsonIgnore]
    internal Boolean IsSearchable => Type == ColumnType.String || Type == ColumnType.Name
        || Type == ColumnType.Memo || Type == ColumnType.Autonum;
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

/* What a posting iterates: ONE details collection, and which of its kinds.
 *
 * One collection is not a restriction, it is the shape - the generated statement makes a
 * single join and the 'row' overrides resolve against that collection's columns. Spelled as a
 * flat list of row sets it would be expressible to name two, and the ban would have to live in
 * the validator; named this way there is nothing to ban.
 *
 * Both parts are written as they are declared - the key of the collection, the keys of the
 * kinds - and nothing here is composed. The composed name of a row set ('StockRows') belongs
 * to the generated side; putting it in metadata would mean the composition rule lives twice,
 * once in the generator and once in whoever writes the file.
 *
 * Only emptiness is checked, never presence: Kinds defaults to [], so an absent key and an
 * empty one are the same value and no rule can tell them apart. A collection with kinds needs
 * a non-empty list (empty would be the wildcard, and a kind added later would silently join
 * the posting); a collection without kinds needs an empty one.
 */
public sealed record PostEachMetadata
{
    public String Details { get; init; } = default!;
    public List<String> Kinds { get; init; } = [];
}

/* The procedures behind a posting the platform does not write itself. A pair, because a procedure
 * that writes the journals owns the inverse too - and 'unpost' is optional: a procedure that wrote
 * the provenance is undone by the same derived delete as any other posting, and only an inverse
 * that is not a delete (a reversal that keeps the rows) has to be authored.
 */
public sealed record PostSqlMetadata
{
    public String Post { get; init; } = default!;
    // 'unpost' in the file; the camel-case strategy would otherwise ask for 'unPost'
    [JsonProperty("unpost")]
    public String? UnPost { get; init; }
}

/* One posting, in one of two spellings: a leg into a journal that the platform maps, or a procedure
 * that posts the whole document. Which key is written decides every other key of the entry, and
 * DeclarationMetadata.CheckPost refuses the combinations that mean nothing.
 */
public sealed record PostMetadata
{
    #region JSON Fields
    public String? Journal { get; init; }
    public PostDirection Dir { get; init; }
    public Boolean Storno { get; init; }
    public PostEachMetadata? Each { get; init; }
    public Dictionary<String, String> Document { get; init; } = [];
    public Dictionary<String, String> Row { get; init; } = [];

    public PostSqlMetadata? Sql { get; init; }
    /* What the procedure writes into, named because nothing can read it out of the procedure. The
     * transactions dialog is built from this list, and so is the unpost when it is not declared.
     */
    public List<String> Journals { get; init; } = [];
    #endregion

    [JsonIgnore]
    public TableMetadata? JournalTable { get; set; }
    [JsonIgnore]
    public List<TableMetadata> JournalTables { get; set; } = [];
    [JsonIgnore]
    public TableMetadata JournalTableCheck => JournalTable ?? throw new InvalidOperationException($"RefTable for '{Journal}' is null");
    [JsonIgnore]
    public Boolean IsSql => Sql != null;

    // the journals this posting touches, whoever writes them: what reads the RESULT counts tables
    [JsonIgnore]
    internal IEnumerable<TableMetadata> Targets => IsSql ? JournalTables : [JournalTableCheck];

    [JsonIgnore]
    public Int16 InOutInt => Dir switch { PostDirection.In => 1, PostDirection.Out => -1, _ => 0 };
}



public enum InitialSource
{
    Literal,
    Context,
    Profile,
    Policy,
    Sql
}

/* What the SHAPE has. Printing is deliberately not here: which blanks exist is declared by the
 * endpoint ('printForms'), and one shape shared by several operations cannot answer it - they
 * print different papers. See CLAUDE.md, "Commands".
 */
public enum TableTrait
{
    Audit,
    Hierarchy,
    Folders,
    Tags,
    Attachments,
}

/* The value of a 'kinds' entry, from the shape side - and it is empty on purpose.
 *
 * A kind splits nothing in storage: one table, one set of columns, one table type. What it
 * does split is the model - rows of each kind arrive as their own collection - so the KEYS are
 * shape (the value set of the RowKind column, the CHECK constraint, the composition of the
 * envelope) and the value is behavior. The same text is deserialized into DeclarationMetadata
 * as well, and that is where a kind's rules are read from; here the value is deliberately
 * unreadable, so there is no second place to look for them.
 */
public sealed record TableKindMetadata;

public sealed record TableMetadata
{
    #region Database fields
    public EndpointKind Kind { get; set; }
    public String Schema { get; set; } = default!;
    public String Table { get; set; } = default!;
    public String Model { get; set; } = default!;
    public String Path { get; set; } = default!;
    public String Label { get; set; } = default!;
    [JsonProperty("fields")]
    private Dictionary<String, TableColumn> _fields { get; init; } = [];

    [JsonIgnore]
    public List<TableColumn> Columns => [.. _fields.Select(
        kp => { kp.Value.Name = kp.Key; return kp.Value; }
    )];
    public Dictionary<String, TableMetadata> Details { get; private set; } = [];
    public Dictionary<String, TableKindMetadata> Kinds { get; init; } = [];
    /* The rows of a set, in the shape and not in the declaration: they are deployed with the table
     * and the whole deploy pipeline is a function of TableMetadata. 'Kinds' above is the same kind
     * of thing - a closed vocabulary declared with the shape, ordered by the order it is written in.
     */
    public List<EnumValueMetadata> Values { get; init; } = [];

    /* Rows deployed with the table, so they are the shape's - the reason 'Values' is here too. A
     * key of its own and not 'values': another record shape, and one key shaped by the endpoint
     * kind is two questions under one name. The cost is that every such registry buys a key here
     * and a branch in the deploy - there is no shared 'rows the file declares' mechanism, and a
     * third one is where writing it would start to pay. See CLAUDE.md, "Autonums".
     *
     * A map and not a list, unlike 'values': there the position IS data (it becomes Order), here
     * nothing but the key means anything - so the key stays outside, as it does in 'fields'. The
     * price is that two identical keys are silently collapsed to the last by the parser, where a
     * list could be checked; that is the trade the format already makes everywhere else.
     */
    [JsonProperty("autonums")]
    private Dictionary<String, AutonumMetadata> _autonums { get; init; } = [];

    [JsonIgnore]
    public List<AutonumMetadata> Autonums => [.. _autonums.Select(
        kp => { kp.Value.Id = kp.Key; return kp.Value; }
    )];
    public List<TableTrait> Traits { get; init; } = [];

    // for sql
    [JsonIgnore]
    public String TypeName => $"T{Model}";
    [JsonIgnore]
    public String RefTypeName => $"TR{Model}";
    [JsonIgnore]
    public String CollectionName => Model.Plural();

    /* The key this collection was declared under in Details. Not the same thing as
     * CollectionName: that one is derived from Model, this one is what the author wrote, and
     * the envelope has always used the written key.
     */
    [JsonIgnore]
    public String DetailsKey { get; private set; } = String.Empty;

    /* Both names of one kind, and neither part is a constant.
     *
     * The collection is kind + the declared key ('Stock' + 'Rows'), the row type is 'T' + kind
     * + the singular model ('T' + 'Stock' + 'Row'). A second collection declared as 'Links'
     * with model 'Link' therefore gives StockLinks/TStockLink from the very same rule - which
     * is the whole reason the parts are read off the declaration instead of being spelled out.
     */
    public String KindCollectionName(String kind) => $"{kind}{DetailsKey}";
    public String KindTypeName(String kind) => $"T{kind}{Model}";

    /* Tab state is per collection, not per document: the strip switching the kinds of 'Rows'
     * has nothing to say about 'Links'. So there are as many state properties as there are
     * collections, each named after its own, holding the name of the collection on screen.
     *
     * 'First' is declaration order - Kinds is filled from the JSON object and never has an
     * entry removed, so the order the author wrote the kinds in is the order of the tabs.
     */
    public String TabStateName => $"$$Tab{DetailsKey}";
    public String FirstTabName => Kinds.Count > 0 ? KindCollectionName(Kinds.Keys.First()) : DetailsKey;

    /* A row set is addressed by its PARTS - the collection key and the kind key, both written
     * exactly as they were declared. The composed name ('StockRows') is the generated side and
     * never appears in metadata: written there, the composition rule would live twice, once in
     * the generator and once in whoever writes the file, with nothing to keep them equal.
     *
     * The form's (scope, kind) and post's (details, kinds) ask the same question, so they ask
     * it here.
     */
    internal TableMetadata FindDetails(String details)
    {
        if (Details.TryGetValue(details, out var d))
            return d;
        throw new InvalidOperationException(ComposedRowSetHint(details)
            ?? $"Details '{details}' not found in {Path}. Available: {String.Join(", ", Details.Keys)}");
    }

    /* The one wrong spelling that is not a typo, and therefore the only one worth its own
     * message: the name the generated side calls a row set by. It is built from the two parts
     * that ARE written, so a reader who has met it in the model, in a tab or in a table type will
     * try it here - and 'Available: Rows' answers a question they did not ask, twice, because
     * their name looks nothing like a misspelling of it.
     *
     * Only kinded collections can produce the confusion: without kinds the composed name and the
     * declared key are the same string, so there is nothing to tell apart.
     */
    private String? ComposedRowSetHint(String name)
    {
        foreach (var (key, detail) in Details)
            foreach (var kind in detail.Kinds.Keys)
                if (detail.KindCollectionName(kind) == name)
                    return $"'{name}' is the name of the generated side (model property, tab value, "
                        + $"table type) and is never written in metadata. Name its parts instead: "
                        + $"scope '{key}', kind '{kind}'.";
        return null;
    }

    /* Only emptiness is examined, never presence - an absent key and an empty list are one
     * value (see PostEachMetadata). Both directions throw, and the first one is the load
     * bearing half: a kind added later must not join an existing posting or an existing tab
     * without any file changing.
     */
    internal void CheckKinds(IReadOnlyList<String> named)
    {
        if (Kinds.Count > 0 && named.Count == 0)
            throw new InvalidOperationException(
                $"'{DetailsKey}' declares kinds ({String.Join(", ", Kinds.Keys)}); name the ones meant - 'all of them' has no spelling");
        if (Kinds.Count == 0 && named.Count > 0)
            throw new InvalidOperationException(
                $"'{DetailsKey}' declares no kinds, but [{String.Join(", ", named)}] were named");
        foreach (var k in named)
            if (!Kinds.ContainsKey(k))
                throw new InvalidOperationException(
                    $"'{DetailsKey}': kind '{k}' not declared. Available: {String.Join(", ", Kinds.Keys)}");
    }

    // the name the row set has on the generated side - model property, tab state value, TVP
    internal String RowSetName(String? kind) => kind == null ? DetailsKey : KindCollectionName(kind);

    /* Every row set of this collection, with the names the generated side calls it by. A
     * collection without kinds is one row set, not a special case - which is what lets the
     * emitting code above be written once instead of branching on Kinds.Count everywhere.
     */
    internal IEnumerable<(String? Kind, String Collection, String Type)> RowSets() =>
        Kinds.Count == 0
            ? [(null, DetailsKey, TypeName)]
            : Kinds.Keys.Select(k => ((String?)k, KindCollectionName(k), KindTypeName(k)));

    [JsonIgnore]
    public Boolean EditWithPage => IsDocument;


    [JsonIgnore]
    public Boolean HasTags => Traits.Contains(TableTrait.Tags);
    public Boolean HasFolders => Traits.Contains(TableTrait.Folders);

    #endregion

    // Service variables
    [JsonIgnore]
    public String SqlSchema => Schema.ToSqlSchema();
    [JsonIgnore]
    public String SqlTableName => $"{SqlSchema}.[{Table}]";
    /* Indexes the PLATFORM needs, not indexes an application wants: code-only, no file key. An
     * index is safe to hold here in a way a primary key was not - nothing else in the platform
     * reads one, so this cannot become a knob that only the DDL honours. What it does buy is a
     * uniqueness the generated SQL can then lean on.
     */
    [JsonIgnore]
    public List<TableIndex> Indexes { get; init; } = [];

    [JsonIgnore]
    public String SqlSequenceName => $"{SqlSchema}.[SQ_{Table}]";
    [JsonIgnore]
    internal String SqlTableTypeName => $"{SqlSchema}.[{Model}.Meta.TableType]";
    [JsonIgnore]
    public String? FileHash { get; set; }

    internal String RowKindField => Columns.FirstOrDefault(c => c.Type == ColumnType.RowKind)?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a RowKind column");

    [JsonIgnore]
    internal Boolean IsCatalog => Kind == EndpointKind.Catalog;
    [JsonIgnore]
    internal Boolean IsDocument => Kind == EndpointKind.Document;
    [JsonIgnore]
    internal Boolean IsJournal => Kind == EndpointKind.Journal;
    [JsonIgnore]
    internal Boolean IsTags => Kind == EndpointKind.Tags;
    [JsonIgnore]
    internal Boolean IsTagEntries => Kind == EndpointKind.TagEntries;
    [JsonIgnore]
    internal Boolean HasPeriod => IsDocument || IsJournal;

    internal void SetDetailDefaults(TableMetadata table, String key)
    {
        Schema = table.Schema;
        Kind = EndpointKind.Details;
        DetailsKey = key;
        Table = $"{table.Model}{key}";
    }
    internal void SetDefaults(String schema, String table)
    {
        // the file that declares this table; spelled like EndpointMetadata.Path, because a
        // DocumentType discriminator is this value and has to be comparable to an address
        Path = String.IsNullOrEmpty(table) ? $"/{schema}" : $"/{schema}/{table}";
        /* One registry at one address, so all three are defaults nobody needs to write; written,
         * they win. The rows land where documents do - a namespace of its own is the address, not
         * their home. The price is this 'if': the method now knows one namespace by name, and the
         * next registry adds a branch here rather than following a rule.
         */
        if (schema == Constants.SchemaNames.Autonum)
        {
            if (String.IsNullOrEmpty(Schema))
                Schema = Constants.SchemaNames.Document;
            if (String.IsNullOrEmpty(Model))
                Model = TableMetadataDefaults.AutonumModel;
            if (String.IsNullOrEmpty(Table))
                Table = TableMetadataDefaults.AutonumTable;
        }
        if (String.IsNullOrEmpty(Schema))
            Schema = schema;
        /* No default for Table. Pluralising the folder name looks like a convention but is a
         * guess: English plurals are irregular, and the name has to be reproduced exactly
         * wherever it surfaces later - migrations, deploy, ejected SQL, legacy mapping - where
         * a near miss creates a second table instead of failing. DatabaseMetadataProvider
         * requires it to be declared, so an empty Table here belongs to a kind that has none.
         */
        /* Kebab, not just capitalized: the folder is an address segment and may be written
         * 'vat-rates', while Model is the stem of a TYPE name (TVatRates, TRVatRates) and of the
         * collection. A dash survives every quoted place it lands in and fails in the unquoted
         * ones - a generated class name among them - which is a break far from its cause.
         */
        if (String.IsNullOrEmpty(Model))
            Model = table.KebabToPascal();
        if (Kind == EndpointKind.Undefined)
            Kind = schema.ToEndpointKind();
        if (String.IsNullOrEmpty(Label))
            Label = Constants.FieldNames.Name;

        foreach (var d in Details)
            d.Value.SetDetailDefaults(this, d.Key);
    }
}
/* One index. The name is not written: it is composed from the table and the columns, so two
 * declarations of the same index are the same object and a renamed column cannot leave a name
 * behind that says something else.
 */
public sealed record TableIndex(Boolean Unique, String[] Columns)
{
    internal String Name(TableMetadata table) =>
        $"{(Unique ? "UX" : "IX")}_{table.Table}_{String.Join('_', Columns)}";
}

public record OperationMetadata(String Id, String? Name, String? Category);

/* One value of a set. Only 'id' is required: 'name' defaults to the localization key
 * '@[{Model}.{Id}]' (the key must carry the set's name, or two 'Complete' in two sets collapse
 * into one translation), and 'order' is not written at all - it is the position in the list, the
 * same rule the tabs of 'kinds' follow.
 *
 * 'void' is a withdrawn value: it stays in the records that already carry it and leaves the list
 * of candidates.
 */
public record EnumValueMetadata(String Id, String? Name, String? Memo, Boolean Void);

/* What the counter restarts on. Default None, because only that half is silent: a yearly reset
 * under a pattern with no year reissues last year's numbers; never restarting only grows.
 */
public enum AutonumPeriod
{
    None,
    Year,
    Quarter,
    Month
}

/* One numbering: the key an endpoint's 'autonum' names, the pattern its numbers are built from,
 * the span its counter restarts on. 'Name' defaults to '@[{Model}.{Id}]', as an enum value's does.
 * No 'void' - nobody picks a numbering at run time, a file names it - and the price is that a key
 * deleted from the file leaves a row nothing marks as dead; its counter has to outlive the key
 * anyway. Renaming one costs more than it looks: the counters stay under the old key and the
 * numbering silently restarts from one.
 */
public record AutonumMetadata
{
    // from the key of the map, the way TableColumn takes its Name
    public String Id { get; set; } = default!;
    public String? Name { get; init; }
    public String Pattern { get; init; } = default!;
    public AutonumPeriod Period { get; init; }
}


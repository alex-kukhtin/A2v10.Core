// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using Newtonsoft.Json;

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
    State,
    Autonum,
    AutonumValues,
    AccPlan,
    Ledger
}
public enum ColumnType
{
    // semantic types
    String, // DEFAULT VALUE!!!
    Id,
    /* A key with a meaning outside the database - an account code, written by a file or a person.
     * The same role as Id (the primary key, never updated, [Id!!Id] in the model), a different
     * domain: nvarchar and no sequence. Two types and not a facet on Id: a length turning platformid
     * into nvarchar is a discriminator nobody sees. The role is asked by TableColumn.IsKey.
     */
    NaturalKey,
    Name,
    Memo,
    RowNumber,
    Done,
    Void,
    IsSystem,
    /* The link from a row to the record it is PART of - a details row to its header, a tag entry
     * to the tagged record. Never declared in a file: the platform emits it, names it after the
     * master's Model and finds it back by this type. The word 'Owner' deliberately no longer
     * means this: belonging (a subordinate catalog to the entity it belongs to) is a different
     * relation - declared target, several per table, an entity of its own - and it gets the word.
     */
    Master,
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
    /* A reference to a set of STATES (/state/...). Keyed by a code exactly as an enum is, and a
     * type of its own all the same: the column has to SAY that the record it sits on has a life
     * cycle, instead of making every reader walk RefTable.Storage.Kind to find out - behaviour
     * dispatches on the type here, where everything else in the platform dispatches. One check
     * comes free with it: a state column whose target is not a set of states fails at load.
     */
    State,
    /* A reference to an account of a chart (/accplan/...): keyed by the account code, so spelled as
     * that key and not as platformid - which is why it is a domain of its own and not a Ref. Its own
     * behaviour (the tree picker, 'subtree') is not built yet.
     */
    Account,
    Autonum,
    Company,
    Direction,  // journal leg sign (+1/-1); vocabulary (In/Out, Dt/Ct) is presentation
    /* A stamp is who + when, written by the platform and never sent by the client. Four types and
     * not two, because nullability is answered by type (DeployNullable): the posting stamp is empty
     * on a draft, the creation and modification stamps never are. WHICH stamp a column is, is its
     * name - every statement writing one names it (Constants.FieldNames), as posting names Done.
     * 'Who' is bigint and not platformid: a login lives in a2security.Users, whatever base the
     * application rests on.
     */
    StampUser,
    StampDate,
    StampUserNull,
    StampDateNull,
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
    public String Name { get; private set; } = default!;
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
    internal Boolean IsRef => Type == ColumnType.Ref || Type == ColumnType.Master ||
            Type == ColumnType.User || Type == ColumnType.Document ||
            Type == ColumnType.Company || Type == ColumnType.Operation ||
            Type == ColumnType.Enum || Type == ColumnType.State || Type == ColumnType.Account ||
            Type == ColumnType.Folder;

    #region Database Fields
    public Int32? Length { get; init; }
    public Int32? Precision { get; init; }
    public Int32? Scale { get; init; }
    /* The key a self link points at, so Parent is spelled as the key of its own table. Set by the
     * platform where the table is in hand (TableDefaultColumns), never in a file.
     */
    [JsonIgnore]
    internal ColumnType KeyType { get; init; } = ColumnType.Id;
    /* No 'Required' here: it is a rule, not a property of the column - see
     * DeclarationMetadata.RuleSet and MetadataExtensions.RequiredFields.
     */
    // OLD -> to RULES
    public Boolean Unique { get; init; }
    #endregion
    [JsonIgnore]
    internal Boolean IsKey => Type is ColumnType.Id or ColumnType.NaturalKey;
    /* A reference to a SET - a closed list of values declared in a file, which rides with the page
     * whole: there is nothing to browse, and the value travels as its own code. Deliberately not
     * 'keyed by a code': an account and an operation are keyed by one too, and both are fetched by
     * address like any catalog. Asked wherever that difference shows - the search join, the sort,
     * the filter's 'All' row, the type of the parameter - so the sites read one word instead of
     * each comparing types and drifting apart.
     */
    internal Boolean IsSetRef => Type is ColumnType.Enum or ColumnType.State;
    internal Boolean IsOperation => Type == ColumnType.Operation;

    [JsonIgnore]
    internal Boolean HasDefaultBit =>
        Type == ColumnType.IsSystem
        || Type == ColumnType.Void
        || Type == ColumnType.Done;

    [JsonIgnore]
    internal Boolean IsStamp => Type is ColumnType.StampUser or ColumnType.StampDate
        or ColumnType.StampUserNull or ColumnType.StampDateNull;

    [JsonIgnore]
    internal Boolean IsVoid => Type == ColumnType.Void;
    [JsonIgnore]
    internal Boolean IsSearchable => Type == ColumnType.String || Type == ColumnType.Name
        || Type == ColumnType.Memo || Type == ColumnType.Autonum;
    [JsonIgnore]
    internal Boolean IsMemo => Type == ColumnType.Memo;
    [JsonIgnore]
    // a natural key is a code the user reads, not the '#' of a surrogate
    internal String Header => Type == ColumnType.NaturalKey ? "@[Code]" : $"@[{Name}]";
    /* The property the column is under in the model. Its own name, except where the data model reserves
     * the word: 'Parent' is every element's link to its container, so a self link travels as 'ParentElem'.
     */
    [JsonIgnore]
    internal String ModelName => Type == ColumnType.Parent ? Constants.FieldNames.ParentElem : Name;
    // a reference shows its 'Name' property: the resolver fills it from the target's Presentation
    internal String DisplayPath => (IsRef) ? $"{Name}.{Constants.FieldNames.Name}" : ModelName;

    internal void SetName(String name) => Name = name;
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

/* One leg of a ledger posting: where each column of the leg takes its value. The source is the key
 * of the block, never the look of the value - a literal and a field name are both strings.
 */
public sealed record PostLegMetadata
{
    public Dictionary<String, String> Const { get; init; } = [];
    public Dictionary<String, String> Document { get; init; } = [];
    public Dictionary<String, String> Row { get; init; } = [];
}

/* One posting, in one of three spellings: a leg into a journal that the platform maps, a posting
 * into a ledger (two mirrored legs from one declaration), or a procedure that posts the whole
 * document. Which key is written decides every other key of the entry, and
 * DeclarationMetadata.CheckPost refuses the combinations that mean nothing.
 */
public sealed record PostMetadata
{
    #region JSON Fields
    public String? Journal { get; init; }
    public String? Ledger { get; init; }
    // one sum for both legs - the balance is structural; the field is of the rows under 'each'
    public String? Sum { get; init; }
    public PostLegMetadata? Dt { get; init; }
    public PostLegMetadata? Ct { get; init; }
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
    public TableMetadata? TargetTable { get; set; }
    [JsonIgnore]
    public List<TableMetadata> SqlTargets { get; set; } = [];
    [JsonIgnore]
    public TableMetadata TargetTableCheck => TargetTable ?? throw new InvalidOperationException($"Target table for '{TargetPath}' is null");
    [JsonIgnore]
    public Boolean IsSql => Sql != null;
    [JsonIgnore]
    public Boolean IsLedger => Ledger != null;
    // the path a mapped entry writes to - a journal or a ledger
    [JsonIgnore]
    internal String? TargetPath => Journal ?? Ledger;

    // the journals this posting touches, whoever writes them: what reads the RESULT counts tables
    [JsonIgnore]
    internal IEnumerable<TableMetadata> Targets => IsSql ? SqlTargets : [TargetTableCheck];

    [JsonIgnore]
    public Int16 InOutInt => Dir switch { PostDirection.In => 1, PostDirection.Out => -1, _ => 0 };
}



public enum InitialSource
{
    Literal,
    Context,
    Profile,
    Policy,
    Sql,
    // a parameter of the url that opens creation; the value is its name, owned by whoever builds the url
    Query
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
    /* The column a row of this table is shown by wherever it is referenced. One column and not a
     * template: it is spelled into SQL (resolve, sort, search), where a template would mean formatting
     * values in the server's locale. A composite display is two columns in the markup.
     */
    // empty for a kind no reference points at, as Table is for a kind that has none - see SetDefaults
    [JsonProperty("presentation")]
    internal String Presentation { get; private set; } = default!;

    [JsonProperty("fields")]
    private Dictionary<String, TableColumn> _fields { get; init; } = [];

    // the authored columns, materialized by Construct
    [JsonIgnore]
    public IReadOnlyList<TableColumn> Columns { get; private set; } = default!;
    public Dictionary<String, TableMetadata> Details { get; private set; } = [];
    public Dictionary<String, TableKindMetadata> Kinds { get; init; } = [];
    /* The rows of a set, in the shape and not in the declaration: they are deployed with the table
     * and the whole deploy pipeline is a function of TableMetadata. 'Kinds' above is the same kind
     * of thing - a closed vocabulary declared with the shape, ordered by the order it is written in.
     */
    public List<SetValueMetadata> Values { get; init; } = [];

    /* The rows of the operation registry. Not declared by any file as rows - a document lists its
     * operations, or is one itself over a document storage - so the deploy walk fills it
     * (AllElementsMetadata) and json never can.
     */
    [JsonIgnore]
    public List<OperationMetadata> Operations { get; init; } = [];

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

    /* The file beside metadata.json that holds the rows the deploy merges into this table. A key
     * of every table, processed per kind: today only a chart of accounts reads it, and the load
     * refuses it anywhere else (DatabaseMetadataProvider.LoadSeedAsync).
     */
    public String? Seed { get; init; }

    /* The chart a ledger posts against - the target of its Acc and CorrAcc. One chart per ledger: a
     * Plan column would make every foreign key composite. Required on a ledger and refused elsewhere
     * (DatabaseMetadataProvider.CheckAccPlan). The key is named for the kind of its target.
     */
    [JsonProperty("accplan")]
    public String? AccPlan { get; init; }

    // the rows of that file, sorted by key - filled by the load, before the table is published
    [JsonIgnore]
    public List<SeedRow> SeedRows { get; internal set; } = [];

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

    /* The name of the link back to the header, and it is the MASTER'S Model - so a waybill's rows
     * carry [Waybill], not one word repeated in every details table in the database. One rule for
     * every link column then: it is named for what it points at, the way refs already are.
     *
     * Held rather than derived because the column is emitted from the hanging table alone
     * (DetailsDefaultColumns, TagsEntriesDefaultColumns), which does not know its master; this is
     * set where the master IS in hand, beside Table, which is built from the same Model. Everything
     * else asks here, and the places that used to spell the name literally now find the column the
     * way the platform finds every other one - by its type.
     *
     * Two setters, one per satellite kind (SetDetailDefaults, CreateTagEntriesTable), and nothing
     * else may write it: a table that hangs under nobody has no master field, and an empty string
     * is that answer rather than a name nobody chose.
     */
    [JsonIgnore]
    public String MasterField { get; internal set; } = String.Empty;

    /* Both names of one kind, and neither part is a constant.
     *
     * The collection is kind + the declared key ('Stock' + 'Rows'), the row type is 'T' + kind
     * + the singular model ('T' + 'Stock' + 'Row'). A second collection declared as 'Links'
     * with model 'Link' therefore gives StockLinks/TStockLink from the very same rule - which
     * is the whole reason the parts are read off the declaration instead of being spelled out.
     */
    public String KindCollectionName(String kind) => $"{kind}{DetailsKey}";
    public String KindTypeName(String kind) => $"T{kind}{Model}";

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
    // the stamps come with the kind (TableDefaultColumns), so the table answers by its columns
    [JsonIgnore]
    internal Boolean HasStamps => this.AllColumns(c => c.IsStamp).Any();

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
    /* The type the rows of this table arrive in, named by the PATH in the model and not by the
     * model alone: a type name is global to its schema, while a collection's model is a word
     * inside its master's model. Every document writes "model": "Row" - that is the natural word -
     * so the unqualified name gave every one of them doc.[Row.Meta.TableType]: the deploy emits
     * drop + create per collection, the last shape wins, and the saves of the others hand a
     * DataTable of another shape to a type that no longer describes it. The table itself never
     * collided, because SetDetailDefaults prefixes it with the master's model.
     *
     * Written rather than derived, for the reason MasterField is: the hanging table does not know
     * its master, and the one place that does is the factory below - so nothing has to be held
     * for a reader that only composes a name.
     */
    [JsonIgnore]
    internal String SqlTableTypeName { get; private set; } = default!;
    [JsonIgnore]
    public String? FileHash { get; set; }

    /* The baseline, materialized by Construct. Built once and not per call: a column is an object the
     * load writes into (RefTable), and a fresh one per call would lose it.
     */
    [JsonIgnore]
    internal IReadOnlyList<TableColumn> DefaultColumns { get; private set; } = default!;

    /* Every input of the baseline (Kind, Traits, MasterField) is set before the call, and so are
     * the two the type name is composed of. The master's model is a PARAMETER and not a member:
     * it is an input of one name and nothing else reads it, so a caller that has the master hands
     * it over instead of every table carrying a field for it. Null is the answer of everything
     * whose model already stands alone in its schema: a shape addressed by a folder, and the two
     * satellites built in code (tag entries, autonum counters), whose model is composed from the
     * master's at the factory.
     */
    internal void Construct(String? masterModel = null)
    {
        DefaultColumns = [.. this.CreateDefaultColumns()];
        Columns = [.. _fields.Select(kp => { kp.Value.SetName(kp.Key); return kp.Value; })];
        var path = masterModel == null ? Model : $"{masterModel}.{Model}";
        SqlTableTypeName = $"{SqlSchema}.[{path}.Meta.TableType]";
    }

    // the primary key: always named Id (CreateTable), found by its role like every other column
    [JsonIgnore]
    internal TableColumn KeyColumn => this.AllColumns().First(c => c.IsKey);

    internal String RowKindField =>Columns.FirstOrDefault(c => c.Type == ColumnType.RowKind)?.Name
        ?? throw new InvalidOperationException($"The table {SqlTableName} does not have a RowKind column");

    [JsonIgnore]
    internal Boolean IsCatalog => Kind == EndpointKind.Catalog;
    [JsonIgnore]
    internal Boolean IsDocument => Kind == EndpointKind.Document;
    [JsonIgnore]
    internal Boolean IsJournal => Kind == EndpointKind.Journal;
    [JsonIgnore]
    internal Boolean IsLedger => Kind == EndpointKind.Ledger;
    /* A set: rows declared in the file, deployed with the table, no screen of its own, reached only
     * through the columns pointing at it. Asked by everything that walks values, so that a kind
     * added to the family is added here and not to a comparison in five generators.
     */
    [JsonIgnore]
    internal Boolean IsSet => Kind is EndpointKind.Enum or EndpointKind.State;
    [JsonIgnore]
    internal Boolean IsState => Kind == EndpointKind.State;
    [JsonIgnore]
    internal Boolean IsTags => Kind == EndpointKind.Tags;
    [JsonIgnore]
    internal Boolean IsTagEntries => Kind == EndpointKind.TagEntries;
    [JsonIgnore]
    internal Boolean HasPeriod => IsDocument || IsJournal || IsLedger;

    /* The colour a ROW draws in. A property of the row and not of the reference to it, so it is
     * asked of the shape and never of the kind: a state carries one because its baseline does, a
     * catalog because the author declared the column, and both paint the same badge wherever the
     * row is shown. That is why nothing has to be declared on the referring side - the colour
     * travels with every resolve (SqlBuilder.RefFields) and with every candidate list.
     *
     * Two such columns are not an answer: which of them is the row's colour would be a guess, so
     * none is, and each then draws as an ordinary column of its own. Said here rather than at the
     * three readers, so they cannot drift on what 'has a colour' means.
     */
    [JsonIgnore]
    internal TableColumn? ColorColumn
    {
        get
        {
            var colors = this.AllColumns(c => c.Type == ColumnType.Color).Take(2).ToList();
            return colors.Count == 1 ? colors[0] : null;
        }
    }

    internal void SetDetailDefaults(TableMetadata table, String key)
    {
        Schema = table.Schema;
        Kind = EndpointKind.Details;
        DetailsKey = key;
        MasterField = table.Model;
        Table = $"{table.Model}{key}";
        Construct(table.Model);
    }
    /* The folders of a catalog: declared by the owner's trait and served by the owner's actions, so
     * the owner's address is theirs too. Shown by Name - Folder is a reference, and the map resolves
     * it by the presentation like any other.
     */
    internal void SetFolderDefaults(TableMetadata owner)
    {
        Kind = EndpointKind.Folders;
        Schema = owner.Schema;
        Model = $"{owner.Model}Folder";
        Table = $"{owner.Model}$Folders";
        Path = owner.Path;
        Construct();
        Presentation = Constants.FieldNames.Name;
    }
    internal void SetDefaults(String schema, String table) => SetDefaults(schema, schema, table);

    /* 'schema' is the kind the folder is of, 'folder' where the file lies - one word unless an
     * alias (app.json) puts a kind's endpoints into a folder of another name. Everything below is
     * defaulted from the kind; only Path is the folder's.
     */
    internal void SetDefaults(String schema, String folder, String table)
    {
        // the file that declares this table; spelled like EndpointMetadata.Path, because a
        // DocumentType discriminator is this value and has to be comparable to an address
        Path = String.IsNullOrEmpty(table) ? $"/{folder}" : $"/{folder}/{table}";
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
        /* A set of states is named after the ENTITY whose states they are - /state/order - and its
         * model may not be that entity's word. Model is the stem of five names, and one of them is
         * the collection the candidates arrive in: they arrive in the root of the model of the very
         * page that shows the record, where 'Orders' is already the records themselves. Two arrays
         * under one name, two TROrder in one .d.ts, '@[Order.packed]' over the document's own keys -
         * all silent, and all unavoidable, because a set of states is always named after something
         * that has a screen. An enum escapes it by being named after a concept ('vatrate'), not
         * after an owner.
         *
         * So the default is composed, not the folder alone. Not a guess of the kind refused for
         * table names: nothing is inflected, and 'order' + 'State' is reproducible in the head. A
         * written 'model' still wins - this only makes the readable address safe by default.
         */
        if (schema == Constants.SchemaNames.State && String.IsNullOrEmpty(Model))
            Model = $"{table.KebabToPascal()}State";
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
        Construct();

        /* Only the kinds a reference points at are shown by anything; a journal or the numbering
         * registry is nobody's target, so 'presentation' written there is refused rather than left a
         * key with no effect. Not written: each kind answers for itself - an account is known by its
         * code, a document by its number, the rest by Name. A natural key is not a code the user reads
         * by itself: an enum's key is written by the file and read by nobody. Written: it must be a
         * column, because downstream it is an identifier inside SQL.
         */
        TableColumn? column(ColumnType type) => this.AllColumns().FirstOrDefault(c => c.Type == type);

        void present(TableColumn? byDefault)
        {
            if (String.IsNullOrEmpty(Presentation))
                Presentation = byDefault?.Name
                    ?? throw new InvalidOperationException(
                        $"{Path}: nothing to be shown by - declare 'presentation', or add a Name or autonum column");
            else if (!this.AllColumns().Any(c => c.Name == Presentation))
                throw new InvalidOperationException($"{Path}: presentation '{Presentation}' is not a column of this table");
        }

        switch (Kind)
        {
            case EndpointKind.Catalog:
            case EndpointKind.Enum:
            case EndpointKind.State:
            case EndpointKind.Operation:
                present(column(ColumnType.Name));
                break;
            case EndpointKind.Document:
                present(column(ColumnType.Name) ?? column(ColumnType.Autonum));
                break;
            case EndpointKind.AccPlan:
                present(column(ColumnType.NaturalKey));
                break;
            default:
                if (!String.IsNullOrEmpty(Presentation))
                    throw new InvalidOperationException($"{Path}: 'presentation' on a {Kind} - nothing references it, so nothing is shown by it");
                break;
        }

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

/* One row of the operation registry: the code (see MetadataExtensions.DocumentOperations), the
 * document it belongs to and its place in that document's list. The last two are a projection of
 * the files, kept by the deploy, so SQL finds a document's operations by equality and in order
 * instead of spelling the list out in every statement.
 */
public record OperationMetadata(String Id, String Document, Int32 Order);

/* One value of a set. Only 'id' is required: 'name' defaults to the localization key
 * '@[{Model}.{Id}]' (the key must carry the set's name, or two 'Complete' in two sets collapse
 * into one translation), and 'order' is not written at all - it is the position in the list, the
 * same rule the tabs of 'kinds' follow.
 *
 * 'void' is a withdrawn value: it stays in the records that already carry it and leaves the list
 * of candidates.
 *
 * The last two belong to a set of STATES and are refused on an enum (CheckValues). Both are
 * written in the file exactly as they will be stored, because nothing translates them on the way:
 * 'role' lands in the column and in a generated predicate, so it is spelled as the member of
 * StateRole; 'color' goes straight into a CSS class, so it is lower case. One is a name of the
 * platform and the other a name of the stylesheet - hence the two registers in one row.
 */
public record SetValueMetadata(String Id, String? Name, String? Memo, Boolean Void,
    String? Color, StateRole? Role);

/* One row of a seed file: the key, and the columns the row names with their values as written.
 * A column the row does not name is absent from Values, not null - the merge leaves it untouched.
 */
public sealed record SeedRow(String Id, IReadOnlyDictionary<String, String?> Values);

/* The section of the reports an account belongs to. Stored by NAME, as AutonumPeriod is.
 * OffBalance is a value here and not a flag: an off-balance account is neither an asset nor a
 * liability, so the two axes coincide.
 */
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense,
    OffBalance
}

/* The side the balance of an account is shown on. Not derived from AccountType: accumulated
 * depreciation is an Asset with a Credit balance.
 */
public enum NormalBalance
{
    Debit,
    Credit,
    Both
}

/* What a value of a set of states IS to the cycle - the whole reason the kind exists beside an
 * enum, together with the colour. Stored by name, as AccountType is.
 *
 * The cardinalities are unequal, and that is the content: exactly one Initial (a new record has to
 * start somewhere definite) and exactly one Success, any number of InProgress and of Failure.
 * Success is MEASURED, so it is one - 'did it get there' becomes a single comparison instead of a
 * test for membership, and the conversion of the whole set falls out with nothing declared.
 * Failure is CLASSIFIED, so there are many - 'customer refused', 'no budget', 'duplicate' are
 * reasons, not outcomes. The price is named: a cycle with two different good endings must pick one
 * Success and tell the rest apart by a field of its own.
 *
 * 'InProgress' and not 'Processing': the other three are a word each, and the -ing one read as
 * 'something is processing this record right now' - a job, not a phase of the cycle. Two words
 * joined are already the shape of a member here (AccountType.OffBalance).
 *
 * Nothing needs to be declared beside it. Terminality is Success/Failure; 'the open ones' is
 * Initial/InProgress, generated as a predicate over the values - a 'Closed' column would be a
 * second Done. Where a new record starts is Initial, so an endpoint declaring an initial value for
 * a state column is a second spelling of the same fact and is refused.
 */
public enum StateRole
{
    Initial,
    InProgress,
    Success,
    Failure
}

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
 * the span its counter restarts on. 'Name' defaults to '@[{Model}.{Id}]', as a set value's does.
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


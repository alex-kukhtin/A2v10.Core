// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

namespace A2v10.Metadata;

/* What the endpoint's own metadata.json declares, as opposed to the shape it works on. The same
 * text is deserialized twice - once into TableMetadata, once into this - and each type picks up
 * the keys it knows. 'table' is deliberately in both: here it answers 'was it written', there
 * 'what is it'. See CLAUDE.md, "Declarations".
 */

/* The 'rules' block: the key is the KIND of rule and the field names live inside it, so each kind
 * carries its own shape. See CLAUDE.md, "Declarations".
 */
public abstract record RuleSet
{
    public String[] Required { get; init; } = [];

    /* Which members the ARRAY of a row set carries a sum for: a total is a member of the
     * collection, where 'computed' is a member of the element. It says nothing about storage.
     */
    public String[] Total { get; init; } = [];
    public Dictionary<String, String> Visible { get; init; } = [];
    public Dictionary<String, String> Computed { get; init; } = [];
    public Dictionary<String, InheritMetadata> Inherit { get; init; } = [];
}

public sealed record RuleMetadata : RuleSet
{
    public List<ConditionalRuleMetadata> When { get; init; } = [];

    /* Rules layer kind by kind: mine wins. Lists union, maps override by key. Called on both
     * layering axes - storage under operation (DatabaseMetadataProvider.MergeDeclaration) and
     * collection under row kind (RulesFor) - so it is one implementation. See CLAUDE.md,
     * "Declarations".
     *
     * 'When' is left all-or-nothing rather than decided: no generator reads it yet, so there is
     * no case in hand to decide its granularity by.
     */
    public static RuleMetadata Merge(RuleMetadata own, RuleMetadata storage) => new()
    {
        Required = [.. storage.Required.Union(own.Required)],
        Total = [.. storage.Total.Union(own.Total)],
        When = own.When.Count > 0 ? own.When : storage.When,
        Visible = ByKey(own.Visible, storage.Visible),
        Computed = ByKey(own.Computed, storage.Computed),
        Inherit = ByKey(own.Inherit, storage.Inherit)
    };

    private static Dictionary<String, T> ByKey<T>(Dictionary<String, T> own, Dictionary<String, T> storage)
    {
        if (storage.Count == 0)
            return own;
        var merged = new Dictionary<String, T>(storage);
        foreach (var (key, value) in own)
            merged[key] = value;
        return merged;
    }
}

/* What one row kind declares. Rules and nothing else: 'post' is what an operation does and
 * 'table'/'storage' is where data lives - neither is a question a subset of rows can answer.
 */
public sealed record KindDeclarationMetadata
{
    public RuleMetadata Rules { get; init; } = new();
}

/* The same kinds under a condition, which scopes rules instead of being a parameter of one ('on
 * credit' adds DueDate and CreditDays).
 *
 * Deliberately NOT derived from RuleMetadata: without a 'When' of its own, nesting has no
 * spelling at all. Depth one is held by the type, not by a rule in the validator.
 */
public sealed record ConditionalRuleMetadata : RuleSet
{
    public String Test { get; init; } = default!;
}

public sealed record InitialMetadata(InitialSource Source, String Value);
public sealed record InheritMetadata(String Ref, String Field);

/* Two columns and a name. 'Field' and 'Ref' belong to the table the rule is written on and are
 * resolved at load; 'Source' belongs to the table the reference points at, reachable only once the
 * reference graph is linked, so it stays a name - RefMapBuilder.BuildInheritStructure resolves it
 * then. See CLAUDE.md, "Declarations".
 */
public sealed record InheritDescriptor(TableColumn Field, TableColumn Ref, String Source);

/* One row set, as everything downstream needs it: the names the generated side calls it by, and
 * the rules in force for it. Built by the bake from the collection and the kind together. A
 * collection without kinds is one row set and not a special case, as TableMetadata.RowSets() has
 * it - the two lists are the same list, one carrying the shape's answer and this one the
 * declaration's.
 *
 * No table here: whoever needs the columns of a row is asking the shape a shape question.
 */
public sealed record RowSetDeclaration(
    String? Kind,
    String Collection,
    String Type,
    RuleMetadata Rules,
    Dictionary<String, InheritDescriptor[]> Inherits);

public sealed record DeclarationMetadata
{
    /* Where the shape this endpoint works on comes from - one axis, three spellings, exactly one
     * of them written. 'Table' names my own table; 'Storage' points at a table declared elsewhere,
     * which I write to; 'Surface' points at a shape I only read. None has a default, so 'empty'
     * here means 'not written' and nothing else, and which of the three is legal is decided by the
     * folder - see DatabaseMetadataProvider.CheckShapeSource.
     */
    public String? Table { get; init; }
    public String? Storage { get; init; }
    public String? Surface { get; init; }

    public Dictionary<String, InitialMetadata> InitialValues { get; init; } = [];
    public RuleMetadata Rules { get; init; } = new();
    public List<PostMetadata>? Post { get; init; }
    public String? Autonum { get; init; }

    // rules and inherit declared for the rows of a detail; keys match TableMetadata.Details
    public Dictionary<String, DeclarationMetadata> Details { get; init; } = [];

    /* Rules of one row kind, on a details declaration. Keys match TableMetadata.Kinds: the value
     * set of the discriminator there, the address of a rule block here.
     */
    public Dictionary<String, KindDeclarationMetadata> Kinds { get; init; } = [];

    /* The forms this endpoint shows, as the file writes them - keyed by the action that opens
     * them, and that key set is closed (see DeclarationBake.BuildForms). Per endpoint rather than
     * per shape - see CLAUDE.md, "Forms: whole or nothing".
     */
    public Dictionary<String, FormMetadata> Forms { get; init; } = [];

    /* The forms in force: declared or default, and every name in them already resolved against the
     * shape. Filled by the bake. Empty for a shape nothing renders (tags, the rows of a
     * collection): a table is deployed whether or not anything shows it.
     *
     * Separate from 'Forms' because declared and resolved never share a field - see CLAUDE.md,
     * "Declarations".
     */
    [JsonIgnore]
    public IReadOnlyDictionary<String, FormMetadata> BakedForms { get; init; }
        = new Dictionary<String, FormMetadata>();

    internal FormMetadata Form(String name) =>
        BakedForms.TryGetValue(name, out var form)
            ? form
            : throw new InvalidOperationException($"The form '{name}' is not built for this endpoint");

    /* The journals this endpoint posts to, once each. Two legs into one journal (in/out, storno)
     * are two postings and one journal: everything that reads the RESULT of posting - the unpost
     * delete, the transactions dialog - counts tables, not postings.
     *
     * Empty when nothing is declared, which is the same answer as 'this document does not post':
     * the command bar asks it that way and so does the dialog.
     */
    internal IEnumerable<TableMetadata> PostJournals() =>
        (Post ?? []).Select(p => p.JournalTableCheck).DistinctBy(j => j.SqlTableName);

    /* The rules in force for one row set. A kind that says nothing is not an empty layer to
     * visit - it is the collection's rules unchanged.
     */
    public RuleMetadata RulesFor(String? kind) =>
        kind != null && Kinds.TryGetValue(kind, out var k) ? RuleMetadata.Merge(k.Rules, Rules) : Rules;

    /* What this node's own rules inherit, keyed by the reference that drives it - one handler per
     * reference, one fetch projection per selector. Read by the ROOT; for a collection the answer
     * is per row set and lives in RowSets.
     */
    [JsonIgnore]
    public Dictionary<String, InheritDescriptor[]> Inherits { get; init; } = [];

    /* The row sets of THIS collection - empty on the root, which is a record and has none. Filled
     * by the bake from the shape, so it covers every row set the shape has and not only the ones
     * the file happened to mention.
     */
    [JsonIgnore]
    public IReadOnlyList<RowSetDeclaration> RowSets { get; init; } = [];

    /* The two ways of pointing elsewhere answer one question, so the loader asks them as one: a
     * path, and the key it was written under. The key travels with the path because every message
     * about it has to name what the author actually wrote.
     */
    [JsonIgnore]
    public String? SharedShape =>
        !String.IsNullOrEmpty(Storage) ? Storage
        : !String.IsNullOrEmpty(Surface) ? Surface
        : null;

    [JsonIgnore]
    public String SharedShapeKey => String.IsNullOrEmpty(Storage) ? "surface" : "storage";

    // my own file declares the shape: nothing to resolve, and it is mine to deploy
    [JsonIgnore]
    public Boolean HasOwnShape => SharedShape is null;
}

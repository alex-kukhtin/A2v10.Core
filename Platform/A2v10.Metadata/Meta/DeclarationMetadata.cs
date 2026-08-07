// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

namespace A2v10.Metadata;

/* What the endpoint's own metadata.json declares, as opposed to the shape it works on.
 *
 * The same text is deserialized twice - once into TableMetadata, once into this - and each
 * type picks up the keys it knows. The split is therefore not a partition of the file: a key
 * may legitimately be declared in both types when there is a real two-layer scenario, and a
 * key belongs in both only under that test, otherwise the question 'where do I read it from'
 * comes back. Today there is exactly one:
 *
 *   'table' - here it answers 'was it written', in TableMetadata 'what is it'. The two
 *             answers are different questions and both are needed: the second one alone
 *             cannot tell a declared name from a derived one.
 *
 * 'inherit' used to be the second, and was not: it is a kind of rule, it lives in 'rules', and
 * the layering it was duplicated for is done once by MergeDeclaration.
 *
 * Always present, for every endpoint. An endpoint that owns its shape reads its declarations
 * from here and its structure from TableMetadata - both parsed from the same file, and the
 * compiler no longer lets the two be confused.
 */

/* The 'rules' block: the key is the KIND of rule, and the field names live inside it. The
 * inverse reading - key is the field, value is a bag of its rules - is what this replaces, and
 * it failed twice for the same reason: a bag has a level on which a parameter can sit without
 * belonging to anything ('applyIf' guarding what?), and every new kind has to widen a shape
 * shared by all of them.
 *
 * With the kind as the unit each kind carries its OWN shape - 'Required' is names and nothing
 * else, 'Visible' and 'Computed' are field-to-expression, 'Inherit' is field-to-source - and a
 * parameter has nowhere to be orphaned: the only place it can be written is inside the shape of
 * its own kind.
 */
public abstract record RuleSet
{
    public String[] Required { get; init; } = [];

    /* Which members the ARRAY of a row set carries a sum for. A separate kind of rule rather
     * than sugar over 'Computed' because the two land on different types: computed is a member
     * of the element, a total is a member of the collection. Naming an aggregate other than a
     * sum is not a longer list here - it is a rule kind that says 'computed, on the array'.
     *
     * It says nothing about storage, which is where it used to live: a bit on the column could
     * not be scoped to a kind, so one kind totalling and its neighbour not was unwritable.
     */
    public String[] Total { get; init; } = [];
    public Dictionary<String, String> Visible { get; init; } = [];
    public Dictionary<String, String> Computed { get; init; } = [];
    public Dictionary<String, InheritMetadata> Inherit { get; init; } = [];
}

public sealed record RuleMetadata : RuleSet
{
    public List<ConditionalRuleMetadata> When { get; init; } = [];

    /* Rules layer kind by kind, and inside a kind at the granularity its shape gives. The law
     * is one sentence - mine wins - and it is written HERE because it is asked on two different
     * axes: storage under operation (DatabaseMetadataProvider.MergeDeclaration) and collection
     * under row kind (DeclarationMetadata.RulesFor). One law, one implementation.
     *
     * 'Required' unions, and the reason is what the layer below MEANS. A list on the layer that
     * speaks about everyone is a statement about all of them, not a default for them, and a
     * baseline defined as true for all is not something one of them gets to weaken: a
     * requirement that does not hold for all is written in the wrong layer, and belongs moved,
     * not overridden.
     *
     * That failure is loud - a layer carrying a requirement it should not shows up as a
     * validator in the generated template. The one this replaces was silent: a lower layer
     * naming a single field of its own dropped the upper list entirely, and nothing anywhere
     * said so. A name repeated in both layers is harmless, Union keeps one.
     *
     * The maps merge by key: visibility of one field and visibility of another are independent
     * facts that happen to share a kind. Overriding is what makes PARTIAL commonality writable
     * - two kinds computing Sum the same way and a third differently is the common formula on
     * the collection and one override, not the formula written twice.
     *
     * 'When' is left all-or-nothing rather than decided: no generator reads it yet, so there is
     * no case in hand to decide its granularity by.
     */
    public static RuleMetadata Merge(RuleMetadata own, RuleMetadata storage) => new()
    {
        Required = [.. storage.Required.Union(own.Required)],
        // same law and the same reason: a list on the layer that speaks about everyone is a
        // statement about all of them, not a default one of them may weaken
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

/* What one row kind declares. Rules and nothing else: 'post' is what an operation does, and
 * 'table'/'storage' is where data lives - neither is a question a subset of rows can answer.
 * Reusing DeclarationMetadata here would make both writable and leave the rejection to a rule.
 */
public sealed record KindDeclarationMetadata
{
    public RuleMetadata Rules { get; init; } = new();
}

/* The same kinds under a condition. A condition is almost never about one field ('on credit'
 * adds DueDate and CreditDays), so it scopes rules instead of being a parameter of one.
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
/* Two columns and a name, and the asymmetry is the point. 'Field' and 'Ref' belong to the table
 * the rule is written on, so they are findable the moment the declaration is built. 'Source'
 * belongs to the table the reference points at, and reaching it means the reference graph -
 * which is linked only after the endpoint has been published, because it is cyclic. Resolving it
 * here would push the whole bake past publication and leave the endpoint mutable for the sake of
 * one field.
 *
 * Nearly every reader wants the name anyway - it is what a handler assigns from and what the
 * fetch projection asks the catalog for. The one that wants the column (RefMapBuilder, which
 * emits SQL against that table) runs long after the graph is complete and asks then, through
 * SourceColumn().
 */
public sealed record InheritDescriptor(TableColumn Field, TableColumn Ref, String Source);

/* One row set, as everything downstream needs it: the names the generated side calls it by, and
 * the rules in force for it. Built once, by the bake, from the collection and the kind together
 * - which is the pairing that used to be redone at every generator, six times, each with its own
 * 'and if nothing was declared' branch.
 *
 * A collection without kinds is one row set and not a special case, exactly as TableMetadata
 * .RowSets() has it; the two lists are the same list, one carrying the shape's answer and this
 * one the declaration's.
 *
 * No table here on purpose. Whoever needs the columns of a row is asking the shape a shape
 * question - the element's TS interface, and nothing else - and asks it of the shape.
 */
public sealed record RowSetDeclaration(
    String? Kind,
    String Collection,
    String Type,
    RuleMetadata Rules,
    Dictionary<String, InheritDescriptor[]> Inherits);

public sealed record DeclarationMetadata
{
    /* Where the data lives - one axis, two spellings, exactly one of them written.
     * 'Table' names my own table, 'Storage' points at one declared elsewhere. Neither has a
     * default, so 'empty' here means 'not written' and nothing else; the pair is checked in
     * DatabaseMetadataProvider.CheckDataLocation.
     */
    public String? Table { get; init; }
    public String? Storage { get; init; }

    public Dictionary<String, InitialMetadata> InitialValues { get; init; } = [];
    public RuleMetadata Rules { get; init; } = new();
    public List<PostMetadata>? Post { get; init; }
    public String? Autonum { get; init; }

    // rules and inherit declared for the rows of a detail; keys match TableMetadata.Details
    public Dictionary<String, DeclarationMetadata> Details { get; init; } = [];

    /* Rules of one row kind, on a details declaration. Keys match TableMetadata.Kinds - the
     * same text is deserialized twice, and the kind keys are read on both sides: as the value
     * set of the discriminator there, as the address of a rule block here.
     */
    public Dictionary<String, KindDeclarationMetadata> Kinds { get; init; } = [];

    /* The rules in force for one row set. A kind that says nothing is not an empty layer to
     * visit - it is the collection's rules unchanged.
     */
    public RuleMetadata RulesFor(String? kind) =>
        kind != null && Kinds.TryGetValue(kind, out var k) ? RuleMetadata.Merge(k.Rules, Rules) : Rules;

    /* What this node's own rules inherit, keyed by the reference that drives it - one handler per
     * reference, one fetch projection per selector, which is how every reader wants it.
     *
     * Read by the ROOT, where it is the record's own answer. For a collection the answer is per
     * row set and lives in RowSets: a kind that says nothing has the collection's, and that is
     * already folded in there rather than being a lookup anyone has to remember to do.
     */
    [JsonIgnore]
    public Dictionary<String, InheritDescriptor[]> Inherits { get; init; } = [];

    /* The row sets of THIS collection - empty on the root, which is a record and has none.
     * Filled by the bake from the shape, so it covers every row set the shape has and not only
     * the ones the file happened to mention.
     */
    [JsonIgnore]
    public IReadOnlyList<RowSetDeclaration> RowSets { get; init; } = [];

    [JsonIgnore]
    public Boolean HasOwnStorage => String.IsNullOrEmpty(Storage);
}

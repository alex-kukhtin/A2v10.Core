// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

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
    public Dictionary<String, String> Visible { get; init; } = [];
    public Dictionary<String, String> Computed { get; init; } = [];
    public Dictionary<String, InheritMetadata> Inherit { get; init; } = [];
}

public sealed record RuleMetadata : RuleSet
{
    public List<ConditionalRuleMetadata> When { get; init; } = [];
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
public sealed record InheritDescriptor(TableColumn Field, TableColumn Ref, TableColumn Source);

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

    [JsonIgnore]
    public Boolean HasOwnStorage => String.IsNullOrEmpty(Storage);
}

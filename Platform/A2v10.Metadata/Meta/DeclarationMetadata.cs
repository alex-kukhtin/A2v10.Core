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
 * comes back. Today there are two:
 *
 *   'inherit' - the storage declares the base, the endpoint overrides it;
 *   'table'   - here it answers 'was it written', in TableMetadata 'what is it'. The two
 *               answers are different questions and both are needed: the second one alone
 *               cannot tell a declared name from a derived one.
 *
 * Always present, for every endpoint. An endpoint that owns its shape reads its declarations
 * from here and its structure from TableMetadata - both parsed from the same file, and the
 * compiler no longer lets the two be confused.
 */

public sealed record RuleMetadata
{
    public String? Value { get; init; }
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
    public Dictionary<String, InheritMetadata> Inherit { get; init; } = [];
    public Dictionary<String, RuleMetadata> Rules { get; init; } = [];
    public String[] Required { get; init; } = [];
    public List<PostMetadata>? Post { get; init; }
    public String? Autonum { get; init; }

    // rules and inherit declared for the rows of a detail; keys match TableMetadata.Details
    public Dictionary<String, DeclarationMetadata> Details { get; init; } = [];

    public String? ItemsLabel { get; init; }
    public String? ItemLabel { get; init; }

    [JsonIgnore]
    public Boolean HasOwnStorage => String.IsNullOrEmpty(Storage);
}

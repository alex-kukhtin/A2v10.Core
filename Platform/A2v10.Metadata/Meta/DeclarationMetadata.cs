// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace A2v10.Metadata;

/* What the endpoint's own metadata.json declares, as opposed to the shape it works on.
 *
 * The same text is deserialized twice - once into TableMetadata, once into this - and each
 * type picks up the keys it knows. The split is therefore not a partition of the file: a key
 * may legitimately be declared in both types when there is a real two-layer scenario. Today
 * that is 'inherit' alone - the storage declares the base, the endpoint overrides it - and a
 * key belongs in both only under that test, otherwise the question 'where do I read it from'
 * comes back.
 *
 * Always present, for every endpoint. An endpoint that owns its shape reads its declarations
 * from here and its structure from TableMetadata - both parsed from the same file, and the
 * compiler no longer lets the two be confused.
 */
public sealed record DeclarationMetadata
{
    // the shape lives elsewhere; empty means 'my own'
    public String? Storage { get; set; }

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

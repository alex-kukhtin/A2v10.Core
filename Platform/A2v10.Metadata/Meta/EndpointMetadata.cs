// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

/* The endpoint container.
 *
 * Everything that belongs to the endpoint itself - where it is addressed, and what
 * its own metadata.json declares - lives here. TableMetadata stays the shape alone:
 * fields, details, table and type names. That split is what makes a shape safe to
 * share: an operation and the document storage behind it point at one and the same
 * TableMetadata instance, and there is nothing per-endpoint left in it to overwrite.
 *
 * Table and Declaration are both always set. An endpoint that owns its shape has
 * them equal - one instance, not a copy; an operation over a shared storage has them
 * different. No consumer asks which case it is in: structure is read from Table,
 * declared behaviour from Declaration, and both roads are always open.
 *
 * Built by the loader and never mutated afterwards. It is not deserialized from JSON,
 * so 'required' costs nothing here and states the invariant instead of documenting it.
 */
public sealed record EndpointMetadata
{
    // identity: the folder the metadata.json was found in
    public required EndpointKind Kind { get; init; }
    public required String Schema { get; init; }
    public required String Name { get; init; }

    public required TableMetadata Table { get; init; }
    public required TableMetadata Declaration { get; init; }

    // property of the file, not of the shape
    public String? FileHash { get; init; }

    /* Not a field. The address is a function of the identity, so there is nothing to
     * assign and nothing for a request to stamp. Note: no trailing slash for a
     * kind-level endpoint ('/document'), unlike TableMetadata.SetDefaults today.
     */
    public String Path => String.IsNullOrEmpty(Name) ? $"/{Schema}" : $"/{Schema}/{Name}";
}

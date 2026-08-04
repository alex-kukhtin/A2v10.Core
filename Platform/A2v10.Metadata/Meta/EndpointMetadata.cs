// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Vml;

namespace A2v10.Metadata;

/* The endpoint container.
 *
 * Everything that belongs to the endpoint itself - where it is addressed, and what its own
 * metadata.json declares - lives here. TableMetadata stays the shape alone: fields, details,
 * table and type names. That split is what makes a table safe to share: an operation and the
 * document storage behind it point at one and the same TableMetadata instance, and there is
 * nothing per-endpoint left in it to overwrite.
 *
 * Built by the loader and never mutated afterwards. It is not deserialized from JSON, so
 * 'required' costs nothing here and states the invariant instead of documenting it.
 *
 * The union is over the BODY, not over the common part: both cases have an identity and a table
 * they work on, and they differ in what the file declares about it. So the type is a
 * discriminator in exactly one place - choosing the builder - and nowhere else. Naming the table
 * slot per case is deliberate: 'Storage' is where my data lives, 'Surface' is what I read; the
 * relation differs even though the type does not.
 */
public abstract record EndpointMetadata
{
    // identity: the folder the metadata.json was found in
    public required EndpointKind Kind { get; init; }
    public required String Schema { get; init; }
    public required String Name { get; init; }

    // property of the file, not of the table
    public String? FileHash { get; init; }

    /* Not a field. The address is a function of the identity, so there is nothing to assign and
     * nothing for a request to stamp.
     */
    public String Path => String.IsNullOrEmpty(Name) ? $"/{Schema}" : $"/{Schema}/{Name}";
}

/* An endpoint over data: catalog, document, operation, journal. Both slots are always set - for
 * an endpoint that owns its table they come from the same file, for an operation from two. No
 * consumer asks which case it is in: structure is read from Storage, declared behaviour from
 * Declaration, and both roads are always open.
 */
public sealed record NormalEndpointMetadata : EndpointMetadata
{
    public required TableMetadata Storage { get; init; }
    public required DeclarationMetadata Declaration { get; init; }
}

/* A report has no data of its own: it is a window into a surface it does not own and never
 * writes to. Which is also why it declares no table and therefore never reaches deploy.
 */
public sealed record ReportEndpointMetadata : EndpointMetadata
{
    public required TableMetadata Surface { get; init; }
    public required ReportMetadata Report { get; init; }
}

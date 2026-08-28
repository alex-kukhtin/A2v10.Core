// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

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

/* What a reference resolves to: an address to browse and a shape to read columns from. Exactly
 * the two things every reader of TableColumn.RefTable asks for, and nothing else - which is what
 * lets an endpoint the platform implements itself be a reference target without any of them
 * learning about it. A report deliberately does not implement it: it owns no data to point at.
 */
public interface IRefTarget
{
    TableMetadata Storage { get; }
    String Path { get; }
}

/* An endpoint over data: catalog, document, operation, journal. Both slots are always set - for
 * an endpoint that owns its table they come from the same file, for an operation from two. No
 * consumer asks which case it is in: structure is read from Storage, declared behaviour from
 * Declaration, and both roads are always open.
 */
public sealed record NormalEndpointMetadata : EndpointMetadata, IRefTarget
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

    private TableColumn FindSurfaceColumn(String columnName)
    {
        return Surface.Columns.FirstOrDefault(c => c.Name == columnName) ??
            throw new InvalidOperationException($"Report '{Path}' refers to unknown column '{columnName}'");
    }
    internal IEnumerable<TableColumn> Filters() => Report.Filters.Select(FindSurfaceColumn);
    internal IEnumerable<TableColumn> Groups() => Report.Groups.Select(FindSurfaceColumn);
}


/* A system endpoint: behaviour in code, no shape, no declaration and no forms. Which is why it is
 * a type and not a kind - the dispatch in AppMetadataBuilder asks what it IS, and nothing has to
 * read a string to find out.
 *
 * Its address is written here once. The control that opens the dialog and the builder that serves
 * it both read it from this type, so the two cannot drift apart.
 */
public sealed record TagEndpointMetadata : EndpointMetadata
{
    internal const String SettingsAction = "settings";

    internal static String SettingsUrl(String forEntity) =>
        $"/{Constants.SchemaNames.Tag}/{SettingsAction}?{Constants.FieldNames.For}={forEntity}";
}

/* The registry of operation codes. Also a system endpoint - nothing describes it, its one screen
 * is written in code - but unlike the tags dialog it IS pointed at: every document's 'operation'
 * column resolves here. So this one carries a shape, and that is the whole difference between the
 * two so far. See CLAUDE.md, "System endpoints".
 */
public sealed record OperationEndpointMetadata : EndpointMetadata, IRefTarget
{
    internal const String BrowseAction = "browse";

    /* Handed in, not built here, and that is not ceremony: one TableMetadata per
     * (dataSource, schema, table) for every endpoint that points at it is what the storage cache
     * is for. A property initializer would make a fresh instance per endpoint and quietly break it.
     */
    public required TableMetadata Storage { get; init; }
}

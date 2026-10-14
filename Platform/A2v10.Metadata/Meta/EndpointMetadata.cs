// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.Infrastructure;

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
    public required String Schema { get; init; }
    public required String Name { get; init; }

    // property of the file, not of the table
    public String? FileHash { get; init; }

    /* 'grants', one row per role. On the base and not on the data endpoint: a right is held on an
     * address, and a report has one. null - the file does not write the key.
     */
    public IReadOnlyList<EndpointGrant>? Grants { get; init; }

    /* Not a field. The address is a function of the identity, so there is nothing to assign and
     * nothing for a request to stamp.
     */
    public String Path => String.IsNullOrEmpty(Name) ? $"/{Schema}" : $"/{Schema}/{Name}";

    /* What the model.json the reader synthesizes for its folder says (design/blobs.md): whether the
     * endpoint lives under ?base=, and its doors. A behaviour, so the type answers
     */
    public virtual Boolean RequireBase => false;
    public virtual IReadOnlyDictionary<String, BlobDoor> Blobs => _noBlobs;
    private static readonly IReadOnlyDictionary<String, BlobDoor> _noBlobs = new Dictionary<String, BlobDoor>();
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

/* The folders of a catalog as a reference target: the rows are $Folders, and there is no address. A
 * folder is reached through the actions of the endpoint that reads the record (browsefolder,
 * fetchfolder) - /catalog/agent for an agent, /catalog/customer for the same row read there. A
 * column of the shared table cannot hold an answer that differs by endpoint; given the owner's,
 * a reader at a second address would route silently wrong. So asking throws.
 */
internal sealed record FoldersTarget(TableMetadata Storage) : IRefTarget
{
    public String Path => throw new NotSupportedException(
        $"{Storage.Path}: folders have no address of their own - ask the endpoint that reads the record");
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

    /* The Image columns of the shape, under its model: every address over one table has them. Root
     * columns only - a row set is another table
     */
    public override IReadOnlyDictionary<String, BlobDoor> Blobs =>
        Storage.Columns.Where(c => c.Type == ColumnType.Image)
            .ToDictionary(c => c.Name, c => new BlobDoor(Storage.Model, Constants.Blobs.ImageAccept, c.MaxSize));
}

/* A report has no data of its own: it is a window into a surface it does not own and never
 * writes to. Which is also why it declares no table and therefore never reaches deploy.
 */
public sealed record ReportEndpointMetadata : EndpointMetadata
{
    public required TableMetadata Surface { get; init; }
    public required ReportMetadata Report { get; init; }
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

/* The attachments of any record: rows of a2blob.BlobLinks under ?base= - the address of the owner's
 * table - and the owner's id (design/blobs.md, attachments). A system endpoint, so it lives under
 * base by its type, and its one door is the upload: no field of any data, so it names no model, and
 * no limits - anything, up to the host's own request limit
 */
public sealed record AttachmentEndpointMetadata : EndpointMetadata
{
    internal const String IndexAction = Constants.Actions.Index;
    internal const String UploadKey = "Upload";

    // the owner rides as the dialog's argument (its id), the base in the query - the owner's table address
    internal static String IndexUrl(String @base) =>
        $"/{Constants.SchemaNames.Attachment}/{IndexAction}?{Constants.FieldNames.BaseQuery}={@base}";

    public override Boolean RequireBase => true;
    public override IReadOnlyDictionary<String, BlobDoor> Blobs => _blobs;
    private static readonly IReadOnlyDictionary<String, BlobDoor> _blobs = new Dictionary<String, BlobDoor>()
    {
        [UploadKey] = new BlobDoor(null, null, null)
    };
}

/* The registry of operation codes. Also a system endpoint - nothing describes it, its one screen
 * is written in code - but unlike the tags dialog it IS pointed at: every document's 'operation'
 * column resolves here. So this one carries a shape, and that is the whole difference between the
 * two so far. See CLAUDE.md, "System endpoints".
 */
public sealed record OperationEndpointMetadata : EndpointMetadata, IRefTarget
{
    internal const String BrowseAction = Constants.Actions.Browse;

    /* Handed in, not built here, and that is not ceremony: one TableMetadata per
     * (dataSource, schema, table) for every endpoint that points at it is what the storage cache
     * is for. A property initializer would make a fresh instance per endpoint and quietly break it.
     */
    public required TableMetadata Storage { get; init; }
}

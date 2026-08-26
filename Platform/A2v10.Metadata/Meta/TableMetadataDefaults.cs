// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class TableMetadataDefaults
{
    /* Tables the platform declares in code instead of reading from a metadata.json. They are not
     * a special kind of endpoint - they have a real table, an address and the same types as
     * everything else; only the source of the declaration differs. So the registry is consulted
     * by the storage loader, and the rest of the pipeline never learns that a file was missing.
     *
     * Keyed by address, and that is the whole point. Sitting in someone else's namespace is what
     * broke the operations registry: at /document/operations the rules of the document namespace
     * applied, GetDefaultStorage handed it the shared documents table, and it became
     * indistinguishable from /document/invoice - the operation filter opened a document dialog.
     * An own namespace removes the ambiguity by construction, without baking a name into any
     * branch.
     */
    public static TableMetadata? SystemTable(String schema, String name)
    {
        return (schema, name) switch
        {
            (Constants.SchemaNames.Operations, "") => OperationsTable(),
            (Constants.SchemaNames.Tags, "") => TagsTable(),
            _ => null
        };
    }

    // doc.[Operations] - the same table the report grouping left-joins to
    private static TableMetadata OperationsTable()
    {
        return new TableMetadata()
        {
            Kind = EndpointKind.Operation,
            Schema = Constants.SchemaNames.Document,
            Model = "Operation",
            Table = "Operations"
        };
    }

    private static TableMetadata TagsTable()
    {
        return new TableMetadata()
        {
            Kind = EndpointKind.Tags,
            Schema = Constants.SchemaNames.Catalog,
            Model = "Tag",
            Table = "$Tags"
        };
    }

    /* Not a registry entry: there is one of these per tagged table, so it is parameterized and
     * has no address of its own.
     */
    public static TableMetadata CreateTagEntriesTable(TableMetadata table)
    {
        return new TableMetadata()
        {
            Kind = EndpointKind.TagEntries,
            Schema = Constants.SchemaNames.Catalog,
            Model = $"{table.Model}TagEntry",
            Table = $"{table.Model}$TagEntries"
        };
    }
}

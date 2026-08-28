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
        // TODO: system SCHEMAS ????
        return (schema, name) switch
        {
            (Constants.SchemaNames.Operations, "") => OperationsTable(),
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

    /* Not a registry entry either, and for a different reason than the tag entries below: /tag is
     * served by TagEndpointMetadata, which never builds a storage, so nothing would ever ask the
     * registry for it. The table itself is alive - the deploy and SqlBuilderTags take it directly.
     */
    public static TableMetadata TagsTable()
    {
        return new TableMetadata()
        {
            Kind = EndpointKind.Tags,
            Schema = Constants.SchemaNames.Catalog,
            Model = "Tag",
            Table = "$Tags"
        };
    }

    /* The tags catalog as the generated SQL names it, and the type its rows arrive under. Both come
     * off TagsTable, so a rename of the model or the schema moves every query with them.
     */
    public static String TagsTableName() => TagsTable().SqlTableName;
    public static String TagsTypeName() => TagsTable().TypeName;

    /* The tag entries table by the owner's MODEL alone, for the one caller that has the name and
     * not the table - the tags dialog, which is handed 'For' and nothing else. It can be built
     * from the model because the schema is not the owner's: see CreateTagEntriesTable.
     */
    public static String TagEntriesTableName(String forModel) =>
        $"{Constants.SchemaNames.Catalog.ToSqlSchema()}.[{forModel}$TagEntries]";

    /* Not a registry entry: there is one of these per tagged table, so it is parameterized and
     * has no address of its own.
     *
     * The schema is CATALOG and not the owner's, whoever the owner is - tag entries of a document
     * still land in 'cat'. Every reader has to take the name from here for that reason; spelling
     * it as '{owner.SqlSchema}.[{Model}$TagEntries]' is right for a catalog by accident and wrong
     * for everyone else.
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

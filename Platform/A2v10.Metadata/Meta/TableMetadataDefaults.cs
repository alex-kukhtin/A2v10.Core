// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Metadata;

internal static class TableMetadataDefaults
{
    /* doc.[Operations] - the same table the report grouping left-joins to, and the shape behind
     * the /operation endpoint. Defaults are applied here because nothing else applies them any
     * more: the table used to reach SetDefaults through BuildStorage, and a system endpoint never
     * goes that way. 'Path' and 'Label' are what that call leaves behind, and both are read later.
     */
    public static TableMetadata OperationsTable()
    {
        var table = new TableMetadata()
        {
            Kind = EndpointKind.Operation,
            Schema = Constants.SchemaNames.Document,
            Model = "Operation",
            Table = "Operations"
        };
        table.SetDefaults(Constants.SchemaNames.Operation, String.Empty);
        return table;
    }

    /* The three names of the numbering registry, in one place: SetDefaults gives them to the file,
     * the deploy writes the procedure with them and the save writes the call. Two files apart, so a
     * literal in each is a drift waiting for a rename.
     */
    public const String AutonumModel = "Autonum";
    public const String AutonumTable = "Autonums";

    public static String AutonumProcedureName() =>
        $"{Constants.SchemaNames.Document.ToSqlSchema()}.[{AutonumModel}.NextValue]";

    /* The counters of that registry - one row per numbering and period. Parameterized like the tag
     * entries and for the same reason: it is built for DDL and never resolved as an endpoint,
     * because nothing addresses a counter.
     */
    public static TableMetadata CreateAutonumValuesTable(TableMetadata table)
    {
        return new TableMetadata()
        {
            Kind = EndpointKind.AutonumValues,
            Schema = table.Schema,
            Model = $"{table.Model}Value",
            Table = $"{table.Model}$Values",
            /* The key that matters here, since the primary key is on the surrogate Id. It is what
             * makes 'merge with (holdlock)' in the issuing procedure lock one key instead of a
             * range, and what turns a counter split by a race into a failed insert.
             */
            Indexes = [new TableIndex(true, [
                Constants.FieldNames.Autonum, Constants.FieldNames.Year,
                Constants.FieldNames.Quart, Constants.FieldNames.Month])]
        };
    }

    /* Not a registry entry either, and for a different reason than the tag entries below: /tag is
     * served by TagEndpointMetadata, which never builds a storage, so nothing would ever ask the
     * registry for it. The table itself is alive - the deploy and SqlBuilderTags take it directly.
     */
    public static TableMetadata TagsTable()
    {
        var table = new TableMetadata()
        {
            Kind = EndpointKind.Tags,
            Schema = Constants.SchemaNames.Catalog,
            Model = "Tag",
            Table = "$Tags"
        };
        // same call its sibling makes: nothing reads Path or Label here yet, and two factories
        // differing without a reason is how the next reader learns the wrong rule
        table.SetDefaults(Constants.SchemaNames.Tag, String.Empty);
        return table;
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

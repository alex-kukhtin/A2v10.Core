// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Metadata;

internal static class TableMetadataDefaults
{
    public static TableMetadata CreateOperationsTable()
    {
        return new TableMetadata()
        {
            Schema = Constants.SchemaNames.Document,
            Model = "Operation",
            Table = "Operations"
        };
    }

    public static TableMetadata CreateTagsTable()
    {
        return new TableMetadata()
        {
            Kind = EndpointKind.Tags,
            Schema = Constants.SchemaNames.Catalog,
            Model = "Tag",
            Table = "Tags"
        };
    }

    public static TableMetadata CreateTagEntriesTable(TableMetadata table)
    {
        return new TableMetadata()
        {
            Kind = EndpointKind.TagEntries,
            Schema = Constants.SchemaNames.Catalog,
            Model = $"{table.Model}TagEntry",
            Table = $"{table.Model}TagEntries"
        };
    }
}

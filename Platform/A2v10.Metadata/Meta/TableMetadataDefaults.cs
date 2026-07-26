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

    public static TableMetadata CreateTagsTable(TableMetadata parent)
    {
        return new TableMetadata()
        {
            Kind = EndpointKind.Tags,
            Schema = parent.Schema,
            Model = "Tags",
            Table = $"{parent.Table}Tags"
        };
    }
}

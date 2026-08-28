// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal static class TableDefaultColumns
{
    internal static IEnumerable<TableColumn> DefaultColumns(this TableMetadata table)
    {
        return table.Kind switch
        {
            EndpointKind.Catalog => CatalogDefaultColumns(table),
            EndpointKind.Document => DocumentDefaultColumns(table),
            EndpointKind.Journal => JournalDefaultColumns(table),
            EndpointKind.Details => DetailsDefaultColumns(table),
            EndpointKind.Operation => OperationDefaultColumns(table),
            EndpointKind.Folders => FolderDefaultColumns(table),
            EndpointKind.Tags => TagsDefaultColumns(table),
            EndpointKind.TagEntries => TagsEntriesDefaultColumns(table),
            _ => throw new InvalidOperationException($"Default columns not defined for {table.Kind}")
        };
    }
    static IEnumerable<TableColumn> CatalogDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Void, ColumnType.Void);
        yield return new TableColumn(Constants.FieldNames.IsSystem, ColumnType.IsSystem);
        yield return new TableColumn(Constants.FieldNames.RowVersion, ColumnType.RowVersion);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
        if (table.Traits.Contains(TableTrait.Hierarchy))
            yield return new TableColumn(Constants.FieldNames.Parent, ColumnType.Parent);
        if (table.HasFolders)
            yield return new TableColumn(Constants.FieldNames.Folder, ColumnType.Folder);
    }
    static IEnumerable<TableColumn> DocumentDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Void, ColumnType.Void);
        yield return new TableColumn(Constants.FieldNames.Done, ColumnType.Done);
        yield return new TableColumn(Constants.FieldNames.Date, ColumnType.Date);
        yield return new TableColumn(Constants.FieldNames.RowVersion, ColumnType.RowVersion);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
    }
    static IEnumerable<TableColumn> JournalDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Date, ColumnType.Date);
    }
    static IEnumerable<TableColumn> DetailsDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Owner, ColumnType.Owner);
        yield return new TableColumn(Constants.FieldNames.RowNo, ColumnType.RowNumber);
    }

    static IEnumerable<TableColumn> OperationDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Operation);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
    }

    static IEnumerable<TableColumn> FolderDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
    }

    static IEnumerable<TableColumn> TagsDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.For, ColumnType.RowKind);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Color, ColumnType.Color);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
    }

    static IEnumerable<TableColumn> TagsEntriesDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Owner, ColumnType.Owner);
        /* No Target on purpose. This table is built for DDL and is never resolved as an endpoint,
         * so the reference has nobody to point at - and a plausible-looking address here would be
         * read as the address of the tags endpoint, which it is not.
         */
        yield return new TableColumn(Constants.FieldNames.Tag, ColumnType.Ref);
    }
}

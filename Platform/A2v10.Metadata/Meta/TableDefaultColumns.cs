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
            EndpointKind.Enum => EnumDefaultColumns(table),
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

    /* A set of codes: the key is the code itself, so it is a string and not ColumnType.Id - which
     * would be platformid, and would bring a sequence default onto a key the declaration writes.
     * Not ColumnType.Enum either: that one means 'a reference to a set', IsRef says yes to it, and
     * a set whose own key is a reference to itself is the double role that was removed elsewhere.
     * The length is the one every discriminator has, so both sides of the FK are spelled by the
     * same ToSqlDbTypeInfo branch.
     */
    static IEnumerable<TableColumn> EnumDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.String) { Length = 64 };
        /* Void and not a bit of its own: 'withdrawn from use' is the same statement the platform
         * already makes about a catalog row, and it is the same everywhere - not null, default 0,
         * never an index column. A value that is void keeps its rows and leaves the candidate list.
         */
        yield return new TableColumn(Constants.FieldNames.Void, ColumnType.Void);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
        yield return new TableColumn(Constants.FieldNames.Order, ColumnType.Integer);
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

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
}

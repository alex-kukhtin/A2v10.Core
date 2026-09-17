// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal static class TableDefaultColumns
{
    // called once, by TableMetadata.Construct: the columns are objects that keep what the load writes into them
    internal static IEnumerable<TableColumn> CreateDefaultColumns(this TableMetadata table)
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
            EndpointKind.Autonum => AutonumDefaultColumns(table),
            EndpointKind.AutonumValues => AutonumValuesDefaultColumns(table),
            EndpointKind.Tags => TagsDefaultColumns(table),
            EndpointKind.TagEntries => TagsEntriesDefaultColumns(table),
            EndpointKind.AccPlan => AccPlanDefaultColumns(table),
            EndpointKind.Ledger => LedgerDefaultColumns(table),
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
        foreach (var stamp in Stamps())
            yield return stamp;
    }
    static IEnumerable<TableColumn> DocumentDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Void, ColumnType.Void);
        yield return new TableColumn(Constants.FieldNames.Done, ColumnType.Done);
        yield return new TableColumn(Constants.FieldNames.Date, ColumnType.Date);
        yield return new TableColumn(Constants.FieldNames.RowVersion, ColumnType.RowVersion);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
        foreach (var stamp in Stamps())
            yield return stamp;
        // who posted - emptied by unpost, which leaves nothing of a posting behind
        yield return new TableColumn(Constants.FieldNames.UserPosted, ColumnType.StampUserNull);
        yield return new TableColumn(Constants.FieldNames.UtcDatePosted, ColumnType.StampDateNull);
    }

    /* Creation and modification, on every record the user keeps - a catalog, a document, and the
     * catalogs a trait brings along (folders, tags). Not on a details row or a tag entry: those are
     * part of a record, and the record carries the stamp. Not on a journal: its rows are rewritten
     * on every posting, and who posted is the document's own stamp.
     */
    static IEnumerable<TableColumn> Stamps()
    {
        yield return new TableColumn(Constants.FieldNames.UserCreated, ColumnType.StampUser);
        yield return new TableColumn(Constants.FieldNames.UtcDateCreated, ColumnType.StampDate);
        yield return new TableColumn(Constants.FieldNames.UserModified, ColumnType.StampUser);
        yield return new TableColumn(Constants.FieldNames.UtcDateModified, ColumnType.StampDate);
    }
    static IEnumerable<TableColumn> JournalDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Date, ColumnType.Date);
    }
    static IEnumerable<TableColumn> DetailsDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(table.MasterField, ColumnType.Master);
        yield return new TableColumn(Constants.FieldNames.RowNo, ColumnType.RowNumber);
    }

    // the key is the operation's code - a NaturalKey, see EnumDefaultColumns
    static IEnumerable<TableColumn> OperationDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.NaturalKey);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
    }

    static IEnumerable<TableColumn> FolderDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
        foreach (var stamp in Stamps())
            yield return stamp;
    }

    /* A set of codes: the key is the code the declaration writes - a NaturalKey, as an account code
     * is. Not ColumnType.Id - platformid, with a sequence default. Not ColumnType.Enum either: that
     * one means 'a reference to a set', IsRef says yes to it, and a set whose own key is a reference
     * to itself is the double role that was removed elsewhere. A reference to the set is spelled by
     * this key (ToSqlDbTypeInfo), so both sides of the FK cannot disagree.
     */
    static IEnumerable<TableColumn> EnumDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.NaturalKey);
        /* Void and not a bit of its own: 'withdrawn from use' is the same statement the platform
         * already makes about a catalog row, and it is the same everywhere - not null, default 0,
         * never an index column. A value that is void keeps its rows and leaves the candidate list.
         */
        yield return new TableColumn(Constants.FieldNames.Void, ColumnType.Void);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
        yield return new TableColumn(Constants.FieldNames.Order, ColumnType.Integer);
    }

    /* The key is a code the file writes, so a NaturalKey - see EnumDefaultColumns, which is this case
     * exactly. Not ColumnType.Autonum: that one means the number OF a document. No 'void' - nobody
     * picks a numbering at run time. No counter column: counters are rows of a table of their own,
     * keyed by numbering and period.
     */
    static IEnumerable<TableColumn> AutonumDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.NaturalKey);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Pattern, ColumnType.String) { Length = 255 };
        /* By NAME, not by the enum's number: reordering AutonumPeriod would rewrite what the rows
         * already mean. Costs a string compare in the issuing SQL and leaves nothing tying the
         * column back to the enum.
         */
        yield return new TableColumn(Constants.FieldNames.Period, ColumnType.String) { Length = 16 };
    }

    /* One counter: a numbering, and the period it counts in. Three columns for the period and not
     * one composed key - the SQL matches them by equality, and composing would put the same rule in
     * the procedure and in whoever reads it. Zero means 'not counted by this', so Period None is
     * (0, 0, 0) and never a null to compare against.
     *
     * No reference to the registry, deliberately: the key is a code, so it cannot ride ColumnType
     * .Ref (platformid), and a foreign key would be wrong anyway - a counter outlives the numbering
     * that is dropped from the file, which is what keeps issued numbers meaningful.
     *
     * 'Id' is here because CreateTable always puts the primary key on it, and that convention is
     * worth more than this one table: made settable, it would be honoured by the DDL alone while
     * every generated statement went on joining by Id. The key that matters - (Autonum, Year,
     * Quart, Month) - is a unique index instead, declared with the table in TableMetadataDefaults.
     */
    static IEnumerable<TableColumn> AutonumValuesDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Autonum, ColumnType.String) { Length = 64 };
        yield return new TableColumn(Constants.FieldNames.Year, ColumnType.Integer);
        yield return new TableColumn(Constants.FieldNames.Quart, ColumnType.Integer);
        yield return new TableColumn(Constants.FieldNames.Month, ColumnType.Integer);
        yield return new TableColumn(Constants.FieldNames.CurrentNumber, ColumnType.Integer);
    }

    /* A chart of accounts. The key is the account code - no Code beside Id, one concept, one name.
     * The tree is in the baseline and Parent is its only carrier: never derived from the code,
     * where a prefix is a convention of one plan. AccountType and NormalBalance are closed sets of
     * the platform, stored by name. IsSystem says the row comes from the seed file and is written
     * by the deploy alone.
     */
    static IEnumerable<TableColumn> AccPlanDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.NaturalKey);
        yield return new TableColumn(Constants.FieldNames.Void, ColumnType.Void);
        yield return new TableColumn(Constants.FieldNames.IsSystem, ColumnType.IsSystem);
        yield return new TableColumn(Constants.FieldNames.RowVersion, ColumnType.RowVersion);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Parent, ColumnType.Parent) { KeyType = ColumnType.NaturalKey };
        yield return new TableColumn(Constants.FieldNames.AccountType, ColumnType.String) { Length = 16 };
        yield return new TableColumn(Constants.FieldNames.NormalBalance, ColumnType.String) { Length = 16 };
        foreach (var stamp in Stamps())
            yield return stamp;
    }

    /* A ledger: one row per leg, two legs per posting. The baseline is what 'post' writes from its
     * own keys (dt/ct, sum), so the platform knows these names ahead. CorrAcc is the Acc of the other
     * leg. The provenance (Document, Operation, Row) is not here: its target is where the documents
     * of the application live, and an application may have documents without operations - so it is
     * declared, as in a journal, and found by type.
     */
    static IEnumerable<TableColumn> LedgerDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.Date, ColumnType.Date);
        yield return new TableColumn(Constants.FieldNames.InOut, ColumnType.Direction);
        yield return new TableColumn(Constants.FieldNames.Acc, ColumnType.Account) { Target = table.AccPlan };
        yield return new TableColumn(Constants.FieldNames.CorrAcc, ColumnType.Account) { Target = table.AccPlan };
        yield return new TableColumn(Constants.FieldNames.Sum, ColumnType.Amount);
    }

    static IEnumerable<TableColumn> TagsDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(Constants.FieldNames.For, ColumnType.RowKind);
        yield return new TableColumn(Constants.FieldNames.Name, ColumnType.Name);
        yield return new TableColumn(Constants.FieldNames.Color, ColumnType.Color);
        yield return new TableColumn(Constants.FieldNames.Memo, ColumnType.Memo);
        foreach (var stamp in Stamps())
            yield return stamp;
    }

    /* Master, like a details row: a tag entry is part of the record it points at, dies with it and
     * is never shown - so it is named by the same rule, after the master's Model.
     */
    static IEnumerable<TableColumn> TagsEntriesDefaultColumns(TableMetadata table)
    {
        yield return new TableColumn(Constants.FieldNames.Id, ColumnType.Id);
        yield return new TableColumn(table.MasterField, ColumnType.Master);
        /* No Target on purpose. This table is built for DDL and is never resolved as an endpoint,
         * so the reference has nobody to point at - and a plausible-looking address here would be
         * read as the address of the tags endpoint, which it is not.
         */
        yield return new TableColumn(Constants.FieldNames.Tag, ColumnType.Ref);
    }
}

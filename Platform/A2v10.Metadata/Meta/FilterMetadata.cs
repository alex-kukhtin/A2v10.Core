// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

// how a filter lands in the WHERE - see CLAUDE.md, "Filters"
public enum FilterKind
{
    Period,
    Ref,
    Tags,
    Set,
    /* By the ROLE of a state rather than by the state itself. Its own kind because the fact is on
     * another table: the WHERE has to go there ('exists'), where every other kind compares a column
     * of this one. What it draws with follows from that and not the other way round.
     */
    Role
}

/* One filter an endpoint has. 'Name' is all three at once: the namespace entry, the property under
 * 'Filter', and what a form writes to reference it. 'Column' only for the kinds a column
 * contributes, and the column itself - never the resolved target, which is linked after
 * publication (see CLAUDE.md, "Declarations").
 */
public sealed record FilterDescriptor(FilterKind Kind, String Name, TableColumn? Column = null)
{
    internal TableColumn ColumnCheck => Column
        ?? throw new InvalidOperationException($"Filter '{Name}' ({Kind}) has no column");
}

internal static class FilterMetadata
{
    /* Which filters this endpoint has - derived, never declared, and read by the index SQL, the
     * CollectionView and the taskpad panel alike. See CLAUDE.md, "Filters".
     *
     * Of the ENDPOINT and not of the shape, because one column means different things over one
     * table: the operation is every document's at the storage (/document), a choice among a few at a
     * document listing its operations, and fixed by the address at a document that is one.
     */
    public static IEnumerable<FilterDescriptor> Filters(this TableMetadata table, DeclarationMetadata declaration)
    {
        if (table.HasPeriod)
            yield return new FilterDescriptor(FilterKind.Period, Constants.FilterNames.Period);

        /* Two kinds, not one with a branch in the control: 'Ref' means the candidates are fetched
         * by address, and a set has no address to fetch from - its whole list rides with the page.
         * The value differs with it (a code, not a reference), so the SQL, the CollectionView and
         * the panel all read one answer instead of each asking 'but is this one a set?'.
         */
        // not the folder: the tree is its filter, and a second one in the panel would say it twice
        foreach (var col in table.AllColumns(c => c.IsRef && c.Type != ColumnType.Folder))
        {
            if (col.IsOperation)
            {
                /* A document listing its operations picks among them as among a set's values - the
                 * list rides with the page (SqlBuilderIndex). A document over a storage that is ONE
                 * operation has nothing to pick: its address already fixes the column.
                 */
                if (declaration.Operations.Count > 0)
                    yield return new FilterDescriptor(FilterKind.Set, col.Name, col);
                else if (declaration.HasOwnShape)
                    yield return new FilterDescriptor(FilterKind.Ref, col.Name, col);
                continue;
            }
            yield return new FilterDescriptor(col.IsSetRef ? FilterKind.Set : FilterKind.Ref, col.Name, col);

            /* A state column contributes two entries, because there are two questions and neither
             * is the other's coarser version: 'which state' picks one of the set, 'which role'
             * picks a stage of the cycle - and the four roles are not four states. A screen shows
             * whichever its form names, and a form can show both; nothing here decides that.
             *
             * Named after the column, so two state columns give four entries that cannot meet.
             * Not checked against the column names of the table - the namespace never has been, and
             * guarding one name of several reads as an invariant while being none (see CLAUDE.md).
             */
            if (col.Type == ColumnType.State)
                yield return new FilterDescriptor(FilterKind.Role,
                    $"{col.Name}{Constants.FieldNames.Role}", col);
        }

        if (table.HasTags)
            yield return new FilterDescriptor(FilterKind.Tags, Constants.FilterNames.Tags);
    }
}

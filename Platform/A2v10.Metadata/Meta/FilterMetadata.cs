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
    Tags
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
     */
    public static IEnumerable<FilterDescriptor> Filters(this TableMetadata table)
    {
        if (table.HasPeriod)
            yield return new FilterDescriptor(FilterKind.Period, Constants.FilterNames.Period);

        foreach (var col in table.AllColumns(c => c.IsRef))
            yield return new FilterDescriptor(FilterKind.Ref, col.Name, col);

        if (table.HasTags)
            yield return new FilterDescriptor(FilterKind.Tags, Constants.FilterNames.Tags);
    }
}

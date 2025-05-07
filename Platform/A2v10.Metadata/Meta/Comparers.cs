// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace A2v10.Metadata;

internal class ColumnReferenceComparer : IEqualityComparer<ColumnReference>
{
    public Boolean Equals(ColumnReference? x, ColumnReference? y)
    {
        if (Object.ReferenceEquals(x, y))
            return true;
        return x?.RefSchema == y?.RefSchema && x?.RefTable == y?.RefTable;
    }

    public Int32 GetHashCode([DisallowNull] ColumnReference column)
    {
        return column.RefSchema.GetHashCode() ^ column.RefTable.GetHashCode();
    }
}

internal class ReportItemMetadataComparer : IEqualityComparer<ReportItemMetadata>
{
    public Boolean Equals(ReportItemMetadata? x, ReportItemMetadata? y)
    {
        if (Object.ReferenceEquals(x, y))
            return true;
        return x?.Column == y?.Column && x?.RefSchema == y?.RefSchema && x?.RefTable == y?.RefTable;
    }

    public int GetHashCode([DisallowNull] ReportItemMetadata item)
    {
        return item.Column.GetHashCode() ^ item.RefSchema.GetHashCode() ^ item.RefTable.GetHashCode();
    }
}

internal static class Comparers
{
    public static ReportItemMetadataComparer ReportItemMetadata { get; } = new();
    public static ColumnReferenceComparer ColumnReference { get; } = new();
}

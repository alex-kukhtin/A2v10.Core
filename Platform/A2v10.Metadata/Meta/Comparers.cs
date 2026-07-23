// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace A2v10.Metadata;

internal class ReportItemMetadataComparer : IEqualityComparer<ReportItemMetadata>
{
    public Boolean Equals(ReportItemMetadata? x, ReportItemMetadata? y)
    {
        if (Object.ReferenceEquals(x, y))
            return true;
        return x?.Column == y?.Column && x?.RealRefSchema == y?.RealRefSchema && x?.RealRefTable == y?.RealRefTable;
    }

    public int GetHashCode([DisallowNull] ReportItemMetadata item)
    {
        return item.Column.GetHashCode() ^ item.RealRefSchema.GetHashCode() ^ item.RealRefTable.GetHashCode();
    }
}

internal static class Comparers
{
    public static ReportItemMetadataComparer ReportItemMetadata { get; } = new();
}

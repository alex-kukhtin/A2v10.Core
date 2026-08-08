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
        return x?.Column == y?.Column && x?.SqlTableName == y?.SqlTableName;
    }

    public int GetHashCode([DisallowNull] ReportItemMetadata item)
    {
        return item.Column.GetHashCode() ^ (item.SqlTableName ?? String.Empty).GetHashCode();
    }
}

internal static class Comparers
{
    public static ReportItemMetadataComparer ReportItemMetadata { get; } = new();
}

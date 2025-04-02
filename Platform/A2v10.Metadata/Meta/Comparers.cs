
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace A2v10.Metadata;

internal class ColumnReferenceComparer : IEqualityComparer<ColumnReference>
{
    public bool Equals(ColumnReference? x, ColumnReference? y)
    {
        return x?.RefSchema == y?.RefSchema && x?.RefTable == y?.RefTable;
    }

    public int GetHashCode([DisallowNull] ColumnReference column)
    {
        return column.GetHashCode();
    }
}

internal class Comparers
{
}

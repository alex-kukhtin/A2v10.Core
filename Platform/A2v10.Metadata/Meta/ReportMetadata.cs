// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Metadata;

/* The report body: the subset of the surface that is offered to the user, plus the layout it is
 * laid out in. The query itself is not here - the client composes it inside this subset.
 *
 * Still the old shape: 'reportItems', a flat list with a G/F/D discriminator, which came from the
 * designer UI that no longer exists. The format it is meant to become - three lists (groups /
 * filters / data) plus defaults, where the key IS the discriminator - is described in the skill's
 * references/report.md and is a separate step: it changes the runtime, not just the type.
 */

public enum ReportItemKind
{
    G,
    F,
    D,
    Grouping = G,
    Filter = F,
    Data = D
}
public record ReportItemMetadata
{
    #region Database Fields 
    public ReportItemKind Kind { get; init; }
    public String Column { get; init; } = default!;
    public ColumnType DataType { get; init; } = default!;
    public String RefSchema { get; init; } = default!;
    public String RefTable { get; init; } = default!;
    public Boolean Checked { get; init; }
    public Int32 Order { get; init; }
    public String? Label { get; init; }
    public String? Func { get; init; }
    #endregion

    public String RealRefSchema => DataType switch
    {
        ColumnType.Operation => "op",
        _ => RefSchema
    };
    public String RealRefTable => DataType switch
    {
        ColumnType.Operation => "operations", // Lower case is important!
        _ => RefTable
    };
}

public sealed record ReportMetadata
{
    // layout discriminator; today 'turnover' is the only one
    public String? Type { get; init; }

    public List<ReportItemMetadata> ReportItems { get; init; } = [];

    public String? ItemLabel { get; init; }
}

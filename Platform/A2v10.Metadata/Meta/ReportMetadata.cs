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


public sealed record ReportDefaultMetadata
{
    public List<String> Groups { get; init; } = [];
    public List<String> Data { get; init; } = [];
}

public sealed record ReportMetadata
{
    /* No 'surface' here. It is read off DeclarationMetadata, with 'table' and 'storage', because
     * all three answer one question and the loader resolves them in one place.
     */

    // layout discriminator; today 'turnover' is the only one
    public String Type { get; init; } = default!;
    public List<String> Groups { get; init; } = [];
    public List<String> Filters { get; init; } = [];
    public List<String> Data { get; init; } = [];
    public ReportDefaultMetadata Defaults { get; init; } = new();

    public String? ItemLabel { get; init; }
}

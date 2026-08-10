using System;

namespace A2v10.Metadata;

public sealed record TableReferrer
{
    public String Schema { get; set; } = default!;
    public String Table { get; set; } = default!;
    public String Column { get; set; } = default!;
    public String SqlTableName => $"{Schema}.[{Table}]";
}

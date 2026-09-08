using System;

namespace A2v10.Metadata;

public sealed record TableReferrer
{
    public String Schema { get; set; } = default!;
    public String Table { get; set; } = default!;
    public String Column { get; set; } = default!;

    // null both when the table hangs under nobody and when the seed has no row for it - the
    // un-joined branch is right for the first and is the only way to hear about the second
    public String? MasterSchema { get; set; }
    public String? MasterTable { get; set; }
    public String? MasterColumn { get; set; }

    public String SqlTableName => $"{Schema}.[{Table}]";
    public String? MasterSqlTableName =>
        String.IsNullOrEmpty(MasterTable) ? null : $"{MasterSchema}.[{MasterTable}]";
}

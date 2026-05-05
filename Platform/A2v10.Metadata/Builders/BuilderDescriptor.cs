using A2v10.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal record BuilderDescriptor
{
    public TableMetadata Table { get; init; } = default!;
    public TableMetadata? BaseTable { get; init; }
    internal AppMetadata AppMeta { get; init; } = default!;
    internal String? DataSource { get; init; }
    internal IPlatformUrl PlatformUrl { get; init; } = default!;
    internal IEnumerable<ReferenceMember> RefFields { get; init; } = default!;
}

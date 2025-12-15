using A2v10.Infrastructure;
using A2v10.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal record ModelJsonViewD
{
    public Boolean Index {  get; init; }
    public String? Template { get; init; }
    public String? View { get; init; }
}

internal record DatabaseMetaD
{
    public String Table { get; set; } = default!;
}

internal record ModelJsonCommandD
{
    public String? Procedure { get; set; }
}

internal record ModelJsonD
{
    public String? Schema {  get; set; }
    public DatabaseMetaD? Meta { get; init; }

    public Dictionary<String, ModelJsonViewD>? Actions { get; init; }
    public Dictionary<String, ModelJsonViewD>? Dialogs { get; init; }
    public Dictionary<String, ModelJsonCommandD>? Commands { get; init; }
}

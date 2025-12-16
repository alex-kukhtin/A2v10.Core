// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal record ModelJsonMetaD { }

internal record ModelJsonViewD
{
    public ModelJsonD? Meta { get; init; }
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

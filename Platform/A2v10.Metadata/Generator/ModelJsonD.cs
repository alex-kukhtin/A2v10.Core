// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace A2v10.Metadata;

internal record ModelJsonViewD
{
    public Boolean Index {  get; init; }
    public String? Template { get; init; }
    public String? View { get; init; }
}

internal record ModelJsonCommandD
{
    public String? Procedure { get; set; }
}

internal record ModelJsonD
{

    [JsonProperty("$schema")]
    public String? RefSchema { get; set; }
    // '$meta': the data stay on the metadata layer, the file only names the screens
    public String? Model { get; set; }

    public Dictionary<String, ModelJsonViewD>? Actions { get; init; }
    public Dictionary<String, ModelJsonViewD>? Dialogs { get; init; }
    public Dictionary<String, ModelJsonCommandD>? Commands { get; init; }
}

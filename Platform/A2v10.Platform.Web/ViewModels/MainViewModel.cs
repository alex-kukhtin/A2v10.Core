// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace A2v10.Platform.Web;
public class MainViewModel
{
    public String? PersonName { get; init; }
    public Boolean Debug { get; init; }
    public String? HelpUrl { get; init; }
    public String? ModelStyles { get; init; }
    public String? ModelScripts { get; init; }
    public Boolean HasNavPane { get; init; }
    public String Theme { get; init; } = String.Empty;
    public Boolean IsUserAdmin { get; init; }
}


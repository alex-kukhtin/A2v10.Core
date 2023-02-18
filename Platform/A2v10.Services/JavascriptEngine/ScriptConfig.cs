// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace A2v10.Services.Javascript;
public class ScriptConfig
{
    private readonly IApplicationHost _host;

    public ScriptConfig(IApplicationHost host)
    {
        _host = host;
    }

#pragma warning disable IDE1006 // Naming Styles
    public ExpandoObject appSettings(String name)
#pragma warning restore IDE1006 // Naming Styles
    {
        return _host.GetEnvironmentObject(name);
    }
}


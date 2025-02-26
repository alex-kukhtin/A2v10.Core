// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;

using A2v10.Infrastructure;

namespace A2v10.Platform;

internal static class OptionsExtensions
{
    public static Dictionary<String, ModuleInfo> ModulesFromString(String modulesString)
    {
        if (String.IsNullOrEmpty(modulesString))
            return null;
        var dict = new Dictionary<String, ModuleInfo>();
        foreach (var m in modulesString.Split(","))
        {
            var name = m.Trim();
            Boolean def = false;
            if (name.EndsWith('*'))
            {
                name = name[..^1];
                def = true;
            }
            dict.Add(name, new ModuleInfo()
            {
                Default = def,
                Path = $"clr-type:App{name}.AppContainer;assembly=App{name}"
            });
        }
        return dict;
    }
}

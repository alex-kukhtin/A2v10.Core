// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;

namespace A2v10.App.Infrastructure;

public class AppMetadataClrOptions
{
    private readonly Dictionary<String, Func<ExpandoObject, IServiceProvider, IClrElement>> _map = new(StringComparer.OrdinalIgnoreCase);

    public void AddElement(String code, Func<ExpandoObject, IServiceProvider, IClrElement> action)
    {
        _map[code] = action;
    }
    public void AddRange(IReadOnlyDictionary<String, Func<ExpandoObject, IServiceProvider, IClrElement>> range)
    {
        foreach (var kvp in range)
            _map[kvp.Key] = kvp.Value;
    }

    public IReadOnlyDictionary<String, Func<ExpandoObject, IServiceProvider, IClrElement>> Map => _map.AsReadOnly();
}

public interface IAppClrProvider
{
    IClrElement? CreateElement(String localPath, ExpandoObject model);
}

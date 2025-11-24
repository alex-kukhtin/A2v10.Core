using System;
using System.Collections.Generic;
using System.Dynamic;

namespace A2v10.Infrastructure.ClrMetadata;


public class AppMetadataClrOptions
{
    private readonly Dictionary<String, Func<ExpandoObject, IServiceProvider, IClrElement>> _map = new(StringComparer.OrdinalIgnoreCase);

    public void AddElement(String code, Func<ExpandoObject, IServiceProvider, IClrElement> action)
    {
        _map[code] = action;
    }

    public IReadOnlyDictionary<String, Func<ExpandoObject, IServiceProvider, IClrElement>> Map => _map.AsReadOnly();
}

public interface IAppClrProvider
{
    IClrElement? CreateElement(String localPath, ExpandoObject model);
}

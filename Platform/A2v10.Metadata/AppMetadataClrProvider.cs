
using System;
using System.Collections.Generic;
using System.Dynamic;

using A2v10.Infrastructure.ClrMetadata;

namespace A2v10.Metadata;

public class AppMetadataClrProvider : IAppClrProvider
{
    private readonly IReadOnlyDictionary<String, Func<ExpandoObject, IServiceProvider, IClrElement>> _elemMap;
    private readonly IServiceProvider _serviceProvider;
    public AppMetadataClrProvider(AppMetadataClrOptions _options, IServiceProvider serviceProvider)
    {
        _elemMap = _options.Map;
        _serviceProvider = serviceProvider;
    }

    #region IAppClrProvider Members
    public IClrElement? CreateElement(String localPath, ExpandoObject model)
    {
        if (_elemMap.TryGetValue(localPath, out var activator))
            return activator(model, _serviceProvider);
        return null;
    }
    #endregion
}

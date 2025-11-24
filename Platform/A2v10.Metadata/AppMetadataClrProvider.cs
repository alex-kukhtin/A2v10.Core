
using System;
using System.Collections.Generic;
using System.Dynamic;

using A2v10.Infrastructure.ClrMetadata;

namespace A2v10.Metadata;

public class AppMetadataClrProvider(AppMetadataClrOptions _options, IServiceProvider _serviceProvider) : IAppClrProvider
{
    private readonly IReadOnlyDictionary<String, Func<ExpandoObject, IServiceProvider, IClrElement>> _elemMap = _options.Map;

    #region IAppClrProvider Members
    public IClrElement? CreateElement(String localPath, ExpandoObject model)
    {
        if (_elemMap.TryGetValue(localPath, out var activator))
            return activator(model, _serviceProvider);
        return null;
    }
    #endregion
}

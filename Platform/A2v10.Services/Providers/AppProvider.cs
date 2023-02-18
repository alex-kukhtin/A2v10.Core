// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Options;

namespace A2v10.Services;

public class AppProvider : IAppProvider
{
    private readonly IAppContainer _appContainer;
    public AppProvider(IOptions<AppOptions> options)
    {
        var path = options.Value.Path;
        var assembly = ClrHelpers.ParseClrType(path);
        var container = Activator.CreateInstance(assembly.assembly, assembly.type)?.Unwrap();
        if (container is IAppContainer appContainer)
            _appContainer = appContainer;
        else
            throw new ArgumentException("Invalid application container");

    }

    public IAppContainer Container => _appContainer;
}

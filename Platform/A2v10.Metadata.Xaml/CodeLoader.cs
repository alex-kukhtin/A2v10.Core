// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal class CodeLoader(IServiceProvider _serviceProvider)
{
    private readonly IAppCodeProvider _codeProvider = _serviceProvider.GetRequiredService<IAppCodeProvider>();
    private readonly IXamlPartProvider _xamlPartProvider = _serviceProvider.GetRequiredService<IXamlPartProvider>();

    /* The same probe the platform makes for every other view (WebViewEngineProvider, Components):
     * '.vxaml' first, then '.xaml'. Asked for '.xaml' alone, a view materialized by the platform
     * itself - which writes '.vxaml' - was never found.
     */
    private static readonly String[] _viewExtensions = [".vxaml", ".xaml"];

    public UIElement LoadPage(IModelView modelView, String viewName)
    {
        foreach (var ext in _viewExtensions)
        {
            var path = _codeProvider.MakePath(modelView.Path, viewName + ext);
            if (!_codeProvider.IsFileExists(path))
                continue;
            var obj = _xamlPartProvider.GetXamlPart(path);
            if (obj is UIElement uIElement)
                return uIElement;
            throw new InvalidOperationException($"Xaml. Root of '{path}' is not a UIElement");
        }
        throw new InvalidOperationException(
            $"The view '{viewName}' was not found in '{modelView.Path}' ({String.Join(", ", _viewExtensions)})");
    }
}

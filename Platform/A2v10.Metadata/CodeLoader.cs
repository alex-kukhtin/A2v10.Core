// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Xaml;

namespace A2v10.Metadata;

internal class CodeLoader(IServiceProvider _serviceProvider)
{
    private readonly IAppCodeProvider _codeProvider = _serviceProvider.GetRequiredService<IAppCodeProvider>();
    private readonly IXamlPartProvider _xamlPartProvider = _serviceProvider.GetRequiredService<IXamlPartProvider>();

    public async Task<String> GetTemplateScriptAsync(IModelView view)
    {
        if (view.Path == null)
            throw new InvalidOperationException("Model.Path is null");
        var pathToRead = _codeProvider.MakePath(view.Path, $"{view.Template}.js");
        using var stream = _codeProvider.FileStreamRO(pathToRead)
            ?? throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
        using var sr = new StreamReader(stream);
        var fileTemplateText = await sr.ReadToEndAsync() ??
            throw new FileNotFoundException($"Template file '{pathToRead}' not found.");
        return fileTemplateText;
    }

    public UIElement LoadPage(IModelView modelView, String viewName)
    {
        var path = _codeProvider.MakePath(modelView.Path, viewName + ".xaml");
        var obj = _xamlPartProvider.GetXamlPart(path);
        if (obj is UIElement uIElement)
            return uIElement;
        throw new InvalidOperationException("Xaml. Root is not an IXamlElement");
    }
}

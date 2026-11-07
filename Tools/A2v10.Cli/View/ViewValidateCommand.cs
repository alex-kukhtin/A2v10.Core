// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Cli;

internal class ViewValidateCommand(IServiceProvider services)
{
    private readonly IXamlPartProvider _xamlPartProvider = services.GetRequiredService<IXamlPartProvider>();
    private readonly IAppCodeProvider _codeProvider = services.GetRequiredService<IAppCodeProvider>();

    // the host's probe (CodeLoader, the view engine): '.xaml' is still loaded, so it is still validated
    private static readonly String[] _viewExtensions = [".vxaml", ".xaml"];

    internal Command Build()
    {
        var cmd = new Command("validate",
            "Validate a view file by loading and instantiating it as the server does: malformed markup, unknown elements or properties, " +
            "bad enum values, missing includes, more than one child in a content property that takes one object. " +
            "Bindings are not checked against the data model.");
        var pathArg = new Argument<String>("view-file")
        {
            Description = "View file, relative to the module root; $prefix for a module. " +
                "Example: catalog/agent/index.view (.vxaml, then .xaml, is appended if needed)."
        };
        cmd.Arguments.Add(pathArg);

        cmd.SetAction(r => Validate(r.GetValue(pathArg)!));
        return cmd;
    }

    private void Validate(String path)
    {
        try
        {
            // a leading slash reads as "from the application root", but resolves as an absolute
            // path - the argument is always relative to the module root, so drop it
            path = path.TrimStart('/', '\\');
            // a view name may contain dots (edit.dialog), so Path.GetExtension is useless here
            if (!_viewExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                path = _viewExtensions.Select(ext => path + ext).FirstOrDefault(_codeProvider.IsFileExists)
                    ?? throw new InvalidOperationException($"The view '{path}' was not found ({String.Join(", ", _viewExtensions)})");
            _xamlPartProvider.GetXamlPart(path);
            JsonResult.Ok(null);
        }
        catch (Exception ex)
        {
            JsonResult.Fail(ex);
        }
    }
}

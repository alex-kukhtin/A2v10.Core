// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.IO;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using A2v10.Infrastructure;

using XamlRenderer = A2v10.Xaml.XamlRenderer;

namespace A2v10.Cli;

internal class ViewValidateCommand(IServiceProvider services)
{
    private readonly IAppCodeProvider _codeProvider = services.GetRequiredService<IAppCodeProvider>();
    private readonly XamlRenderer _renderer = new(services.GetRequiredService<IProfiler>(),
        services.GetRequiredService<IXamlPartProvider>(),
        services.GetRequiredService<ILocalizer>(),
        services.GetRequiredService<IAppCodeProvider>());
    private readonly ClientFramework _framework = services.GetRequiredService<IOptions<AppOptions>>().Value.Framework;

    // the host's probe (CodeLoader, the view engine): '.xaml' is still loaded, so it is still validated
    private static readonly String[] _viewExtensions = [".vxaml", ".xaml"];

    internal Command Build()
    {
        var cmd = new Command("validate",
            "Validate a view file by rendering it as the server does, without data: malformed markup, unknown elements or properties, " +
            "bad enum values, missing includes, more than one child in a content property that takes one object, " +
            "a command without what it requires (CommandName, Url, Report, ...). " +
            "Bindings are not checked against the data model; a branch that depends on data is rendered one way.");
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
            /* Rendered, not only loaded: most of the engine's checks run where the element writes its
             * markup ('CommandName' is required for 'Execute'), and a view that loads may still fail
             * on every open. No data - a CLI has no database to ask.
             */
            var slash = path.Replace('\\', '/').LastIndexOf('/');
            _renderer.Render(new RenderInfo()
            {
                Path = slash < 0 ? String.Empty : path[..slash],
                FileName = path[(slash + 1)..],
                FileTitle = path,
                Framework = _framework
            }, TextWriter.Null);
            JsonResult.Ok(null);
        }
        catch (Exception ex)
        {
            JsonResult.Fail(ex);
        }
    }
}

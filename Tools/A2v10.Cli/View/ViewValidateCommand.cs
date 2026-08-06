// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Cli;

internal class ViewValidateCommand(IServiceProvider services)
{
    private readonly IXamlPartProvider _xamlPartProvider = services.GetRequiredService<IXamlPartProvider>();

    internal Command Build()
    {
        var cmd = new Command("validate",
            "Validate a view file by loading and instantiating it as the server does: malformed markup, unknown elements or properties, " +
            "bad enum values, missing includes. Bindings are not checked against the data model.");
        var pathArg = new Argument<String>("path")
        {
            Description = "Path to the view relative to the module root, $prefix for a module (e.g. catalog/agent/edit.dialog). " +
                "The .vxaml extension is appended if omitted."
        };
        cmd.Arguments.Add(pathArg);

        cmd.SetAction(r => Validate(r.GetValue(pathArg)!));
        return cmd;
    }

    private void Validate(String path)
    {
        try
        {
            // a view name may contain dots (edit.dialog), so Path.GetExtension is useless here
            if (!path.EndsWith(".vxaml", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                path += ".vxaml";
            _xamlPartProvider.GetXamlPart(path);
            JsonResult.Ok(null);
        }
        catch (Exception ex)
        {
            JsonResult.Fail(ex);
        }
    }
}

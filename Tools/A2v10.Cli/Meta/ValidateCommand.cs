// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;

namespace A2v10.Cli;

internal class ValidateCommand(IServiceProvider services)
{
    internal Command Build()
    {
        var verbose = new Option<Boolean>("--verbose", "-v") { Description = "Show verbose output" };
        var cmd = new Command("validate", "validate endpoint");
        cmd.Options.Add(verbose);
        cmd.SetAction(r => r.GetValue(verbose));
        return cmd;
    }
}

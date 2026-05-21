// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace A2v10.Cli;

public class DescribeAction : SynchronousCommandLineAction
{
    public override int Invoke(ParseResult parseResult)
    {
        Console.WriteLine($"describe {parseResult.CommandResult.Command.Name}");
        return 0;
    }
}

public sealed class DescribeOption : Option<Boolean>
{
    private CommandLineAction? _action;

    public DescribeOption()
        : base("--describe", ["-d", "/d"])
    {
        Description = "describe command output";
        Arity = ArgumentArity.Zero;
    }

    public override CommandLineAction? Action
    {
        get => _action ??= new DescribeAction();
        set => _action = value ?? throw new ArgumentNullException(nameof(value));
    }
}

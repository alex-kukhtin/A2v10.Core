// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using CommandLine;

namespace LicenseBuilder;

internal class Program
{
    static void Main(String[] args)
    {
        Parser.Default.ParseArguments<CommandLineOptions>(args)
        .WithParsed<CommandLineOptions>(ProcessCommands);
    }

    static void ProcessCommands(CommandLineOptions opts)
    {
        var cd = Directory.GetCurrentDirectory();
        Console.WriteLine($"Current directory: {cd}");
        var cmdProcessor = new CommandProcessor(cd, opts);
        cmdProcessor.Process();
    }
}

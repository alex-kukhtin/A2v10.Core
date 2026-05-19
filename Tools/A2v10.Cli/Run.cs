// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.CommandLine;
using System.Threading.Tasks;

namespace A2v10.Cli;

/*
* https://learn.microsoft.com/en-us/dotnet/standard/commandline/syntax
*/
internal sealed partial class Program
{
    private Task<int> RunAsync(string[] args)
    {
        var root = new RootCommand("A2v10 platform tools") { 
            Description = "LLM-aware. Structured JSON output. No interactivity."
        };

        // standard platform commands
        var dbCommand = new Command("db")
        {
            Description = "Standard platform commands"
        };
        dbCommand.Subcommands.Add(new TablesCommand(_services).Build());
        dbCommand.Subcommands.Add(new ColumnsCommand(_services).Build());
        root.Subcommands.Add(dbCommand);

        // metadata commands
        var metaCommand = new Command("meta")
        {
            Description = "Metdata-driven platform commands"
        };
        metaCommand.Subcommands.Add(new DeployCommand(_services).Build());
        metaCommand.Subcommands.Add(new ValidateCommand(_services).Build());
        root.Subcommands.Add(metaCommand);
        return root.Parse(args).InvokeAsync();
    }
}
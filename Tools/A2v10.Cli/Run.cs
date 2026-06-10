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
        var root = new RootCommand("LLM-aware. Structured JSON output. No interactivity.");

        // standard platform commands

        // application commands
        var appCommand = new Command("app")
        {
            Description = "Application level platform commands"
        };
        appCommand.Subcommands.Add(new AppConfigCommand(_services).Build());
        root.Subcommands.Add(appCommand);

        // database commands
        var dbCommand = new Command("db")
        {
            Description = "Database commands"
        };
        dbCommand.Subcommands.Add(new TablesCommand(_services).Build());
        dbCommand.Subcommands.Add(new ColumnsCommand(_services).Build());
        dbCommand.Subcommands.Add(new ReferencedByCommand(_services).Build());
        root.Subcommands.Add(dbCommand);

        // endpoint commands
        var endpointCommand = new Command("endpoint")
        {
            Description = "Endpoint commands"
        };
        var mdCommands = new  ResolveEndpointCommand(_services).Register(endpointCommand);
        mdCommands.Add(new EndpointListCommand(_services).Build());
        root.Subcommands.Add(endpointCommand);

        // metadata commands
        var metaCommand = new Command("meta")
        {
            Description = "Metdata-driven platform commands"
        };
        metaCommand.Subcommands.Add(new DeployCommand(_services).Build());
        metaCommand.Subcommands.Add(new EndpointListCommand(_services).Build());
        //root.Subcommands.Add(metaCommand);

        return root.Parse(args).InvokeAsync();
    }
}
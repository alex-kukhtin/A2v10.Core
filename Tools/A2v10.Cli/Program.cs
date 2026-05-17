// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using A2v10.Infrastructure;
using A2v10.Metadata;
using A2v10.Metadata.Cli;
using A2v10.Services;

namespace A2v10.Cli;

/*
* https://learn.microsoft.com/en-us/dotnet/standard/commandline/syntax
*/
internal sealed class Program
{
    static Task<int> Main(string[] args) => new Program().RunAsync(args);

    private readonly IServiceProvider _services;

    private Program()
    {
        var host = Host.CreateApplicationBuilder();

        host.Configuration
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<Program>();

        host.Services
            .AddSingleton<DatabaseMetadataCache>()
            .AddSingleton<IAppCodeProvider, AppCodeProvider>()
            .AddSingleton<DatabaseMetadataProvider>()
            .AddSingleton<CliDatabaseCreator>()
            .AddSingleton<CliDeployDatabase>();

        host.Services.UseSimpleDbContext();

        host.Services.Configure<AppOptions>(opts =>
        {
            host.Configuration.GetSection("application").Bind(opts);
            var strModules = host.Configuration.GetValue<String>("application:modules");

            opts.Modules = host.Configuration.GetSection("application:modules")
                .GetChildren().ToDictionary<IConfigurationSection, String, ModuleInfo>(
                    x => x.Key,
                    x =>
                    {
                        var mi = new ModuleInfo();
                        x.Bind(mi);
                        return mi;
                    },
                    StringComparer.InvariantCultureIgnoreCase);
        });

        host.Services.Configure<DataConfigurationOptions>(opts =>
        {
            opts.ConnectionStringName = "Default";
        });

         _services = host.Build().Services;
    }

    private Task<int> RunAsync(string[] args)
    {
        var root = new RootCommand("A2v10 platform CLI");

        root.Subcommands.Add(new DeployCommand(_services).Build());

        return root.Parse(args).InvokeAsync();
    }
}
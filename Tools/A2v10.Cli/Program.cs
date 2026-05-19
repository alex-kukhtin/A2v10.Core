// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using A2v10.Infrastructure;
using A2v10.Services;
using A2v10.Metadata;
using A2v10.Metadata.Cli;

namespace A2v10.Cli;

/*
* https://learn.microsoft.com/en-us/dotnet/standard/commandline/syntax
*/
internal sealed partial class Program
{
    static Task<int> Main(string[] args) => new Program().RunAsync(args);

    private readonly IServiceProvider _services;

    private Program()
    {
        var host = Host.CreateApplicationBuilder();

        host.Configuration
            .AddJsonFile("WebApp/appsettings.json", optional: false)
            .AddUserSecrets<Program>();

        host.Services.UseSimpleDbContext();

        host.Services
            .AddSingleton<DatabaseMetadataCache>()
            .AddSingleton<IAppCodeProvider, AppCodeProvider>()
            .AddSingleton<DatabaseMetadataProvider>()
            .AddSingleton<CliDatabaseCreator>()
            .AddSingleton<CliDeployDatabase>();




        host.Services.Configure<AppOptions>(opts =>
        {
            host.Configuration.GetSection("application").Bind(opts);
            opts.Modules = host.Configuration.GetSection("application:modules")
                .GetChildren().ToDictionary<IConfigurationSection, String, ModuleInfo>(
                    x => x.Key,
                    x =>
                    {
                        var mi = new ModuleInfo();
                        x.Bind(mi);
                        return new ModuleInfo()
                        {
                            Default = mi.Default,
                            Assembly = mi.Assembly,
                            // CurrentDirectory is Root!!!
                            Path = mi.Path != null ? Path.Combine("WebApp", mi.Path) : null
                        };
                    },
                    StringComparer.InvariantCultureIgnoreCase);
            opts.Environment.Watch = false;
        });


        host.Services.Configure<DataConfigurationOptions>(opts =>
        {
            opts.ConnectionStringName = "Default";
        });

        _services = host.Build().Services;
    }

}
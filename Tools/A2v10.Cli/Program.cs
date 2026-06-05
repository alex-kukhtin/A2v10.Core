// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using A2v10.Infrastructure;
using A2v10.Services;
using A2v10.Metadata;
using A2v10.Metadata.Cli;
using A2v10.Data.Interfaces;
using A2v10.Data.Providers;
using A2v10.ViewEngine.Xaml;
using A2v10.Platform.Web;

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
            .AddSingleton<IDataService, DataService>()
            .AddSingleton<IModelJsonReader, ModelJsonReader>()
            .AddSingleton<IExternalDataProvider, ExternalDataContext>()
            .AddSingleton<IAppVersion, PlatformAppVersion>()
            .AddSingleton<ICurrentUser, CurrentUser>()
            .AddSingleton<ISqlQueryTextProvider, NullSqlQueryTextProvider>()
            .AddSingleton<ILocalizer, EmptyLocalizer>()
            .AddSingleton<DatabaseMetadataProvider>()
            .AddSingleton<CliDatabaseCreator>()
            .AddSingleton<IApplicationHost, WebApplicationHost>()
            .AddSingleton<IDataScripter, VueDataScripter>()
            .AddSingleton<CliDeployDatabase>();

        host.Services.AddSingleton<IAppCodeProvider, AppCodeProvider>()
           .AddSingleton<IModelJsonPartProvider, ModelJsonPartProvider>()
           .AddSingleton<IXamlPartProvider, XamlPartProvider>();

        host.Services.UseAppMetadata();

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

        Console.OutputEncoding = Encoding.UTF8;

        _services = host.Build().Services;
    }

}
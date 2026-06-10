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
    static async Task<int> Main(string[] args) 
    {
        try
        {
            await (new Program().RunAsync(args));
        }
        catch (Exception ex)
        {
            JsonResult.Fail(ex);
        }
        return 0;
    }

    private readonly IServiceProvider _services;

    private static String? ResolveWebAppFolder()
    {
        var dir = Directory.GetCurrentDirectory();
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            var fn = Path.GetFileName(d);
            if (fn.Contains("WebApp", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(d, "appsettings.json")))
                return fn;
        }
        return null;
    }

    private Program()
    {
        var host = Host.CreateApplicationBuilder();

        var webAppFolder = ResolveWebAppFolder() 
            ?? throw new InvalidOperationException("Root host not found");
        host.Configuration
            .AddJsonFile($"{webAppFolder}/appsettings.json", optional: false)
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
            .AddSingleton<CliDeployDatabase>()
            .AddSingleton<HostRoot>((_) => new HostRoot(webAppFolder));

        host.Services.AddSingleton<IAppCodeProvider, AppCodeProvider>()
           .AddSingleton<IModelJsonPartProvider, ModelJsonPartProvider>()
           .AddSingleton<IXamlPartProvider, XamlPartProvider>();

        host.Services.UseAppMetadata();

        host.Services.Configure<AppOptions>(opts =>
        {
            String? getModulePath(ModuleInfo mi)
            {
                const String NULL_MARKER = "null:";
                if (mi.Path == null)
                    return NULL_MARKER;
                if (!String.IsNullOrEmpty(mi.Assembly))
                    return NULL_MARKER;
                if (mi.Path.StartsWith("clr-type:"))
                    return NULL_MARKER;
                return Path.Combine(webAppFolder, mi.Path);
            }

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
                            // CurrentDirectory is Root!!!
                            Path = getModulePath(mi)
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
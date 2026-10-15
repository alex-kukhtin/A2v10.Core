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
using System.Xml.Linq;

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
            // a parse error is the parser's own code; a failed command sets ExitCode in JsonResult.Fail
            var code = await (new Program().RunAsync(args));
            if (code != 0)
                return code;
        }
        catch (Exception ex)
        {
            JsonResult.Fail(ex);
        }
        return Environment.ExitCode;
    }

    private readonly IServiceProvider _services;

    private static String? ResolveWebAppFolder(String dir)
    {
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            var fn = Path.GetFileName(d);
            if (fn.Contains("WebApp", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(d, "appsettings.json")))
                return fn;
            if (fn.Contains("WebApiHost", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(d, "appsettings.json")))
                return fn;
        }
        return null;
    }

    static String? ResolveUserSecretsId(String webAppFolder)
    {
        // one csproj per host folder by design
        var csproj = Directory.EnumerateFiles(webAppFolder, "*.csproj").FirstOrDefault();
        if (csproj is null)
            return null;
        return XDocument.Load(csproj)
            .Descendants("UserSecretsId")
            .FirstOrDefault()?.Value;
    }
    private Program()
    {
        var host = Host.CreateApplicationBuilder();

        var cdPath = Directory.GetCurrentDirectory();
        var webAppFolder = ResolveWebAppFolder(cdPath);
        if (webAppFolder != null)
        {

            host.Configuration
                .AddJsonFile($"{webAppFolder}/appsettings.json", optional: false);

            var userSecretsId = ResolveUserSecretsId(webAppFolder);
            if (!String.IsNullOrEmpty(userSecretsId))
                host.Configuration.AddUserSecrets(userSecretsId);
        }

        host.Services.UseSimpleDbContext();

        // the metadata layer registers itself (UseAppMetadata below); the platform's services keep the host's lifetimes
        host.Services
            .AddScoped<IDataService, DataService>()
            .AddScoped<IModelJsonReader, ModelJsonReader>()
            .AddScoped<IExternalDataProvider, ExternalDataContext>()
            .AddSingleton<IAppVersion, PlatformAppVersion>()
            .AddScoped<ICurrentUser, CurrentUser>()
            .AddSingleton<ISqlQueryTextProvider, NullSqlQueryTextProvider>()
            .AddScoped<ILocalizer, EmptyLocalizer>()
            .AddScoped<IApplicationHost, WebApplicationHost>()
            .AddScoped<IDataScripter, VueDataScripter>()
            .AddSingleton<HostRoot>((_) => new HostRoot(webAppFolder, cdPath));

        host.Services.AddSingleton<IAppCodeProvider, AppCodeProvider>()
           .AddSingleton<IModelJsonPartProvider, ModelJsonPartProvider>()
           .AddSingleton<IXamlPartProvider, XamlPartProvider>();

        // the pages too: validate builds every view, materialize writes one
        host.Services.UseAppMetadata()
            .UseAppMetadataXaml();

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
                return webAppFolder != null ? Path.Combine(webAppFolder, mi.Path) : null;
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

        /* Scopes are checked on every run, not only under Development (the host's default): a singleton
         * holding a scoped service builds silently elsewhere, and 'a2' failed only on a machine that set
         * DOTNET_ENVIRONMENT. RunAsync constructs every command, and each takes its services in the
         * constructor, so every run resolves - and checks - all the CLI uses. Not ValidateOnBuild: it
         * demands what the CLI never asks for (the print handler's report engines). One run is one
         * request, so one scope.
         */
        host.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions()
        {
            ValidateScopes = true
        }));
        _services = host.Build().Services.CreateScope().ServiceProvider;
    }

}
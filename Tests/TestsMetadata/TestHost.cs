// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using A2v10.Data.Interfaces;
using A2v10.Data.Providers;
using A2v10.Infrastructure;
using A2v10.Platform.Web;
using A2v10.Services;
using A2v10.ViewEngine.Xaml;

namespace A2v10.Metadata.Tests;

/* The platform, stood up outside a web host, from this project's OWN appsettings.json.
 *
 * Deliberately not the CLI's bootstrap (Tools/A2v10.Cli/Program.cs): that one hunts for a
 * '*WebApp*' folder under the CURRENT DIRECTORY, which for a test run is the output folder and
 * names nothing. The settings file travels with the assembly instead, and module paths in it are
 * written relative to the project folder - the one place that stays put.
 *
 * One provider for the whole run: the metadata cache is what most invariants are about, and a
 * fresh one per test would make 'loaded once' unaskable.
 */
internal static class TestHost
{
    private static readonly Lazy<IServiceProvider> _services = new(Build, isThreadSafe: true);

    public static IServiceProvider Services => _services.Value;

    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    // bin/<Configuration>/<tfm> -> the project folder
    private static String ProjectDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../.."));

    private static IServiceProvider Build()
    {
        var host = Host.CreateApplicationBuilder();

        host.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false);

        host.Services.UseSimpleDbContext();

        host.Services
            .AddSingleton<IDataService, DataService>()
            .AddSingleton<IModelJsonReader, ModelJsonReader>()
            .AddSingleton<IExternalDataProvider, ExternalDataContext>()
            .AddSingleton<IAppVersion, PlatformAppVersion>()
            .AddSingleton<ICurrentUser, TestCurrentUser>()
            .AddSingleton<ISqlQueryTextProvider, NullSqlQueryTextProvider>()
            .AddSingleton<ILocalizer, TestLocalizer>()
            .AddSingleton<IApplicationHost, WebApplicationHost>()
            .AddSingleton<IDataScripter, VueDataScripter>()
            .AddSingleton<IAppCodeProvider, AppCodeProvider>()
            .AddSingleton<IModelJsonPartProvider, ModelJsonPartProvider>()
            .AddSingleton<IXamlPartProvider, XamlPartProvider>();

        host.Services.UseAppMetadata();

        /* Module paths become ABSOLUTE here. AppCodeProvider resolves a relative one against the
         * current directory, and a test run has no meaningful one - so the only place that knows
         * where this project lives resolves them, once.
         */
        host.Services.Configure<AppOptions>(opts =>
        {
            host.Configuration.GetSection("application").Bind(opts);
            opts.Modules = host.Configuration.GetSection("application:modules")
                .GetChildren()
                .ToDictionary<IConfigurationSection, String, ModuleInfo>(
                    x => x.Key,
                    x =>
                    {
                        var mi = new ModuleInfo();
                        x.Bind(mi);
                        return new ModuleInfo()
                        {
                            Default = mi.Default,
                            Path = mi.Path == null
                                ? null
                                : Path.GetFullPath(Path.Combine(ProjectDir, mi.Path))
                        };
                    },
                    StringComparer.InvariantCultureIgnoreCase);
            opts.Environment.Watch = false;
        });

        host.Services.Configure<DataConfigurationOptions>(opts =>
        {
            opts.ConnectionStringName = "Default";
        });

        return host.Build().Services;
    }
}

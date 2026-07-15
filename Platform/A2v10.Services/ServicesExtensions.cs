// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

using A2v10.Data.Interfaces;
using A2v10.Services;
using A2v10.Services.Api;

namespace Microsoft.Extensions.DependencyInjection;

internal static class OptionsExtensions
{
    public static Dictionary<String, ModuleInfo>? ModulesFromString(String modulesString)
    {
        if (String.IsNullOrEmpty(modulesString))
            return null;
        var dict = new Dictionary<String, ModuleInfo>();
        foreach (var m in modulesString.Split(","))
        {
            var name = m.Trim();
            Boolean def = false;
            if (name.EndsWith('*'))
            {
                name = name[..^1];
                def = true;
            }
            dict.Add(name, new ModuleInfo()
            {
                Default = def,
                Path = $"clr-type:App{name}.AppContainer;assembly=App{name}"
            });
        }
        return dict;
    }
}

public static class ServicesExtensions
{
    public static IServiceCollection UseApiDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAppCodeProvider, AppCodeProvider>()
            .AddSingleton<IModelJsonPartProvider, ModelJsonPartProvider>()
            .AddSingleton<IAppVersion, PlatformAppVersion>();

        services.AddScoped<IDataService, DataService>()
            .AddScoped<IModelJsonReader, ModelJsonReader>()
            .AddScoped<ISqlQueryTextProvider, NullSqlQueryTextProvider>();

        services.AddScoped<ILocalizer, ApiLocalizer>();

        services.TryAddScoped<IExternalDataProvider, NullExternalDataProvider>();

        services.AddSingleton<IXamlPartProvider, NullXamlPartProvider>()
            .AddSingleton<IDataScripter, NullDataScripter>();

        services.AddScoped<ApiDataService>();

        services.Configure<AppOptions>(opts =>
        {
            configuration.GetSection("application").Bind(opts);
            var strModules = configuration.GetValue<String>("application:modules");

            if (strModules != null)
            {
                opts.Modules = OptionsExtensions.ModulesFromString(strModules);
            }
            else
            {
                opts.Modules = configuration.GetSection("application:modules")
                    .GetChildren().ToDictionary<IConfigurationSection, String, ModuleInfo>(
                        x => x.Key,
                        x =>
                        {
                            var mi = new ModuleInfo();
                            x.Bind(mi);
                            return mi;
                        },
                        StringComparer.InvariantCultureIgnoreCase);
            }
        });

        return services;
    }

    public static IServiceCollection ConfigureAppOptions(this IServiceCollection services, IConfiguration configuration, String cookiePrefix)
    {
        services.Configure<AppOptions>(opts =>
        {
            configuration.GetSection("application").Bind(opts);
            opts.CookiePrefix = cookiePrefix;
            var strModules = configuration.GetValue<String>("application:modules");

            if (strModules != null)
            {
                opts.Modules = OptionsExtensions.ModulesFromString(strModules);
            }
            else
            {
                opts.Modules = configuration.GetSection("application:modules")
                    .GetChildren().ToDictionary<IConfigurationSection, String, ModuleInfo>(
                        x => x.Key,
                        x =>
                        {
                            var mi = new ModuleInfo();
                            x.Bind(mi);
                            return mi;
                        },
                        StringComparer.InvariantCultureIgnoreCase);
            }
        });
        return services;
    }
}

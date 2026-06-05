using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.Dynamic;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using A2v10.Infrastructure;

namespace A2v10.Cli;

internal class AppConfigCommand(IServiceProvider services)
{
    private readonly IConfiguration _config = services.GetRequiredService<IConfiguration>();
    private readonly AppOptions _appOptions = services.GetRequiredService<IOptions<AppOptions>>().Value;
    private readonly IHostEnvironment _hostEnvironment = services.GetRequiredService<IHostEnvironment>();

    internal Command Build()
    {
        var cmd = new Command("config", "Show application configuration");

        cmd.SetAction(r => JsonResult.Try(() => AppConfig()));
        return cmd;
    }

    Task<Object> AppConfig()
    {
        var info = new ExpandoObject()
        {
            { "multiTenant", _config.GetValue<Boolean>("Application:MultiTenant") },
            { "metadataEnabled", IsMetatadaEnabled() },
            { "modules", GetModules() }
        };
        return Task.FromResult<Object>(info);
    }

    Boolean IsMetatadaEnabled()
    {
        var path = Path.Combine(_hostEnvironment.ContentRootPath, "WebApp", "WebApp.csproj");
        var doc = XDocument.Load(path);
        return doc.Descendants("PackageReference")
            .Any(x => x.Attribute("Include")?.Value == "A2v10.Metadata");
    }

    List<ExpandoObject> GetModules()
    {
        if (_appOptions.Modules == null)
            return [];

        var cd = _hostEnvironment.ContentRootPath;

        String? getModlulePath(ModuleInfo mi)
        {
            // nullables is significant for LLM
            if (mi.Path is null || !String.IsNullOrEmpty(mi.Assembly))
                return null;
            if (mi.Path.Contains("clr-type:") || mi.Path.StartsWith("null:"))
                return null;
            if (mi.Path.Contains("A2v10."))
                return null;
            var fullPath = Path.GetFullPath(mi.Path);
            return Path.GetRelativePath(cd, fullPath);
        }


        return [.._appOptions.Modules.Select(x => new ExpandoObject()
                {
                    { "prefix", x.Value.Default ? String.Empty : $"${x.Key.ToLowerInvariant()}" },
                    { "root",  getModlulePath(x.Value) }
                }
            ).OrderBy(m => m.Get<String>("prefix"))
        ];
    }
}
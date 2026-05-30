// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.Dynamic;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using A2v10.Infrastructure;

namespace A2v10.Cli;

internal class AppConfigCommand(IServiceProvider services)
{
    private readonly IConfiguration _config = services.GetRequiredService<IConfiguration>();
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
            { "metadataEnabled", IsMetatadaEnabled() }
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
}
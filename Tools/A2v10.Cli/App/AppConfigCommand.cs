// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Cli;

internal class AppConfigCommand(IServiceProvider services)
{
    private readonly IConfiguration _config = services.GetRequiredService<IConfiguration>();

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
            { "metadataEnabled", false }
        };
        return Task.FromResult<Object>(info);
    }
}
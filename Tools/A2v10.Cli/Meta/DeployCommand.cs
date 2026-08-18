// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Metadata;

namespace A2v10.Cli;


public sealed class DeployCommand(IServiceProvider services)
{
    private readonly IConfiguration _config = services.GetRequiredService<IConfiguration>();
    private readonly DatabaseMetadataProvider _metadataProvider = services.GetRequiredService<DatabaseMetadataProvider>();
    private readonly MetadataSupport _metadataSupport = services.GetRequiredService<MetadataSupport>();
    private readonly DbTarget _target = services.GetRequiredService<DbTarget>();
    public Command Build()
    {
        var cmd = new Command("deploy", "Deploy A2v10 application");

        cmd.SetAction(r => DeployDatabase());
        cmd.SetAction(r => JsonResult.Try(() => DeployDatabase()));
        return cmd;
    }

    async Task<Object> DeployDatabase()
    {
        _metadataSupport.EnsureEnabled();
        // the only command that writes to the database - it must never write to a system one
        _target.EnsureNotSystem();

        return await _metadataProvider.DeployDatabaseAllAsync(null); // TODO: DB Schema????
    }
}
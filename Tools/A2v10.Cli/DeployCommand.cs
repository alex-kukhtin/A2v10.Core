// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Metadata;
using A2v10.Metadata.Cli;

namespace A2v10.Cli;

internal sealed class DeployCommand(IServiceProvider services)
{
    private readonly IConfiguration _config = services.GetRequiredService<IConfiguration>();
    private readonly IDbContext _dbContext = services.GetRequiredService<IDbContext>();
    private readonly DatabaseMetadataProvider _metadataProvider = services.GetRequiredService<DatabaseMetadataProvider>();
    private readonly CliDeployDatabase _dbDeploy = services.GetRequiredService<CliDeployDatabase>();
    internal Command Build()
    {
        var part = new Option<String?>("--part", "-p") { Description = "Deploy part" };
        var cmd = new Command("deploy", "Deploy A2v10 app");
        cmd.Options.Add(part);

        cmd.SetAction(r => DeployDatabase(r.GetValue(part)));
        return cmd;
    }

    public async Task DeployDatabase(String? part) 
    {
        Console.WriteLine($"Part: {part}");
        Console.WriteLine($"Current Dir: {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"ConnectionString: {_config.GetConnectionString("Default")}");
        Console.WriteLine($"AppPath: {_config.GetValue<String>("Application:Modules:Main:Path")}");

        var meta = await _metadataProvider.GetSchemaAsync(null, "catalog", "agent");
        Console.WriteLine(meta.SqlTableName);

        var dm = await _dbContext.LoadModelSqlAsync(null, "select top(1) [Agent!TAgent!Object] = null, Id, Name from cat.Agents");

        Console.WriteLine(dm.Root);

        await _dbDeploy.CreateTableType("catalog", "store");
        await _dbDeploy.CreateTableType("catalog", "agent");
        await _dbDeploy.CreateTableType("document", "");

    }
}
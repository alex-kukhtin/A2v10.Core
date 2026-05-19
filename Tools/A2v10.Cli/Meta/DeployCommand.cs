// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Metadata;
using A2v10.Metadata.Cli;
using System.Collections.Generic;

namespace A2v10.Cli;

internal record DeployCommandError
{
    public String Table { get; set; } = default!;
    public String? Ddl { get; set; }
    public String? Message { get; set; }
}
internal record DeployCommandResult
{
    public Boolean Success { get; set; }
    public List<String> Warnings { get; } = [];
    public DeployCommandError? Error { get; set; }
}

public sealed class DeployCommand(IServiceProvider services)
{
    private readonly IConfiguration _config = services.GetRequiredService<IConfiguration>();
    private readonly IDbContext _dbContext = services.GetRequiredService<IDbContext>();
    private readonly DatabaseMetadataProvider _metadataProvider = services.GetRequiredService<DatabaseMetadataProvider>();
    private readonly CliDeployDatabase _dbDeploy = services.GetRequiredService<CliDeployDatabase>();
    public Command Build()
    {
        var verbose = new Option<Boolean>("--verbose", "-v") { Description = "Show verbose output" };
        var cmd = new Command("deploy", "Deploy A2v10 application");
        cmd.Options.Add(verbose);

        cmd.SetAction(r => DeployDatabase(r.GetValue(verbose)));
        return cmd;
    }

    async Task DeployDatabase(Boolean verbose) 
    {
        Console.WriteLine($"Current Dir: {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"ConnectionString: {_config.GetConnectionString("Default")}");
        Console.WriteLine($"AppPath: {_config.GetValue<String>("Application:Modules:Main:Path")}");

        var meta = await _metadataProvider.GetSchemaAsync(null, "catalog", "agent");
        Console.WriteLine(meta.SqlTableName);

        var dm = await _dbContext.LoadModelSqlAsync(null, "select top(1) [Agent!TAgent!Object] = null, Id, Name from cat.Agents");

        Console.WriteLine(dm.Root);

        await _dbDeploy.DeployDatabaseAsync(verbose, msg => Console.WriteLine(msg));

        var dr = new DeployCommandResult();
        var result = JsonConvert.SerializeObject(dr);

        Console.WriteLine(result);

    }
}
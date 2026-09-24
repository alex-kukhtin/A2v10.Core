// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using A2v10.Infrastructure;
using A2v10.Metadata;

namespace A2v10.Cli;

/* Three scripts, one producer each: deploydatabase.sql - the metadata deploy; app.sql - MainApp/sql.json,
 * what is written by hand; full.sql - <host>/sql.json, the platform scripts + deploydatabase.sql + app.sql.
 *
 * Plain: the platform must be in the database already; deploydatabase.sql by the hash, then app.sql
 * always - scripts written by hand are idempotent.
 * --full: an empty database or new package versions, right after the build of the host - the build is
 * what lays fresh platform scripts in _assets/sql. deploydatabase.sql is written, not executed: full.sql
 * executes it, platform first. On an empty database it is not written at all - nothing to generate it
 * against exists yet - and goes in as it lies on disk: committed, or empty for a new application,
 * whose tables the next plain deploy creates.
 */
public sealed class DeployCommand(IServiceProvider services)
{
    private readonly DatabaseMetadataProvider _metadataProvider = services.GetRequiredService<DatabaseMetadataProvider>();
    private readonly SqlDbGenerator _sqlDbGenerator = services.GetRequiredService<SqlDbGenerator>();
    private readonly IAppCodeProvider _codeProvider = services.GetRequiredService<IAppCodeProvider>();
    private readonly IHostEnvironment _hostEnvironment = services.GetRequiredService<IHostEnvironment>();
    private readonly HostRoot _hostRoot = services.GetRequiredService<HostRoot>();

    public Command Build()
    {
        var cmd = new Command("deploy", "Deploy the metadata, then the scripts written by hand (MainApp/sql.json)");
        var fullOption = new Option<Boolean>("--full")
        {
            Description = "Deploy through full.sql, the platform included: an empty database or new package versions. Run right after the build of the host"
        };
        cmd.Options.Add(fullOption);
        cmd.SetAction(r => JsonResult.Try(() => r.GetValue(fullOption) ? DeployFull() : Deploy()));
        return cmd;
    }

    async Task<Object> Deploy()
    {
        EnsureTarget();
        await _metadataProvider.EnsureMetaAsync(null);
        var result = await _metadataProvider.DeployDatabaseAllAsync(null); // TODO: DB Schema????
        foreach (var script in SqlJsonBuilder.Build(MainModuleDir))
            await ExecuteAsync(script);
        return result with { File = Relative(result.File) };
    }

    async Task<Object> DeployFull()
    {
        EnsureTarget();
        if (await _metadataProvider.HasMetaAsync(null))
            await _metadataProvider.WriteDeployDatabaseAllAsync(null);
        SqlJsonBuilder.Build(MainModuleDir);
        var scripts = SqlJsonBuilder.Build(Path.Combine(_hostEnvironment.ContentRootPath, _hostRoot.Host));
        if (scripts.Count == 0)
            throw new InvalidOperationException($"{_hostRoot.Host}/sql.json not found: --full executes the script it builds (full.sql).");
        foreach (var script in scripts)
            await ExecuteAsync(script);
        return new DeployDatabaseResult(Relative(scripts[^1]), true);
    }

    // relative to the current directory, as every path the CLI reports
    String Relative(String path) => Path.GetRelativePath(_hostEnvironment.ContentRootPath, path).NormalizeSlash();

    void EnsureTarget()
    {
        MetadataSupport.Create(services).EnsureEnabled();
        // the only command that writes to the database - it must never write to a system one
        DbTarget.Create(services).EnsureNotSystem();
    }

    String MainModuleDir => Path.GetFullPath(_codeProvider.GetMainModuleFullPath(".", String.Empty));

    // the text of the file, as it lies on disk - the same batches by 'go' as deploydatabase.sql
    Task ExecuteAsync(String path) => _sqlDbGenerator.DeployDatabaseAsync(null, path, File.ReadAllText(path));
}

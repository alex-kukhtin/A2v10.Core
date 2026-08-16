// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Linq;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace A2v10.Cli;

internal sealed record DbTargetInfo(String Server, String Database, String Source);

/*
* The connection target as the configuration sees it: server and database only.
* The connection string itself is never printed - it may carry a password from user secrets.
* Resolved lazily: an unusable connection string must not break the command tree.
*/
internal sealed class DbTarget(IConfiguration config, HostRoot hostRoot)
{
    private const String CONNECTION_STRING_NAME = "Default";
    private const String CONNECTION_STRING_KEY = $"ConnectionStrings:{CONNECTION_STRING_NAME}";

    private static readonly String[] _systemDatabases = ["master", "model", "msdb", "tempdb"];

    private readonly Lazy<DbTargetInfo> _info = new(() => Resolve(config, hostRoot));

    public String Server => _info.Value.Server;
    public String Database => _info.Value.Database;
    // the configuration layer the connection string actually came from - the file to fix
    public String Source => _info.Value.Source;

    public void EnsureNotSystem()
    {
        if (_systemDatabases.Contains(Database, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"The connection string points to the system database [{Database}]. Fix `Database=` in {Source}.");
    }

    private static DbTargetInfo Resolve(IConfiguration config, HostRoot hostRoot)
    {
        var connectionString = config.GetConnectionString(CONNECTION_STRING_NAME)
            ?? throw new InvalidOperationException(
                $"Connection string '{CONNECTION_STRING_NAME}' not found. Add it to ConnectionStrings in {hostRoot.Host}/appsettings.json.");
        var builder = new SqlConnectionStringBuilder(connectionString);
        return new DbTargetInfo(builder.DataSource, builder.InitialCatalog, ResolveSource(config));
    }

    private static String ResolveSource(IConfiguration config)
    {
        if (config is not IConfigurationRoot root)
            return "the application configuration";
        // the last provider that has the key wins - the same rule the configuration itself uses
        var provider = root.Providers.LastOrDefault(p => p.TryGet(CONNECTION_STRING_KEY, out var _));
        if (provider is not FileConfigurationProvider fileProvider)
            return "the application configuration";
        var path = fileProvider.Source.Path;
        if (String.IsNullOrEmpty(path))
            return "the application configuration";
        return Path.GetFileName(path) == "secrets.json"
            ? "user secrets (secrets.json)"
            : path;
    }
}

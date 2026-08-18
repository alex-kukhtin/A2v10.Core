using System;

namespace A2v10.Cli;

internal record HostRoot(String? Source, String CdPath)
{
    public String Host => Source
        ?? throw new InvalidOperationException($"Root host not found. Expected a WebApp or WebApiHost folder in {CdPath} containing appsettings.json.");
}

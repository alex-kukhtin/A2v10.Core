// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Microsoft.Extensions.Hosting;

namespace A2v10.Cli;

internal sealed record HostProject(String Path, Boolean MetadataEnabled);

/*
* Is the application metadata-driven? The host project references A2v10.Metadata - the same
* fact `a2 app config` reports as metadataEnabled. Resolved lazily: the `meta` commands ask
* for it before they touch anything.
*/
internal sealed class MetadataSupport(IHostEnvironment hostEnvironment, HostRoot hostRoot)
{
    private const String METADATA_PACKAGE = "A2v10.Metadata";

    private readonly Lazy<HostProject> _project = new(() => Resolve(hostEnvironment, hostRoot));

    public Boolean IsEnabled => _project.Value.MetadataEnabled;

    public void EnsureEnabled()
    {
        if (!IsEnabled)
            throw new InvalidOperationException(
                $"The application is not metadata-driven: {_project.Value.Path} has no PackageReference to {METADATA_PACKAGE}. The `meta` commands apply to metadata-driven applications only.");
    }

    private static HostProject Resolve(IHostEnvironment hostEnvironment, HostRoot hostRoot)
    {
        var hostFolder = Path.Combine(hostEnvironment.ContentRootPath, hostRoot.Host);
        // one csproj per host folder by design
        var csproj = Directory.EnumerateFiles(hostFolder, "*.csproj").FirstOrDefault()
            ?? throw new InvalidOperationException($"Host project not found. Expected a .csproj in {hostRoot.Host}.");
        var enabled = XDocument.Load(csproj)
            .Descendants("PackageReference")
            .Any(x => x.Attribute("Include")?.Value == METADATA_PACKAGE);
        var path = Path.GetRelativePath(hostEnvironment.ContentRootPath, csproj).Replace('\\', '/');
        return new HostProject(path, enabled);
    }
}

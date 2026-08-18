// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace A2v10.Cli;

/*
* Is the application metadata-driven? The host project references A2v10.Metadata - the same
* fact `a2 app config` reports as metadataEnabled.
*/
internal sealed record MetadataSupport(String ProjectPath, Boolean IsEnabled)
{
    private const String METADATA_PACKAGE = "A2v10.Metadata";

    // created where it is needed, not in DI - nothing here is worth sharing
    internal static MetadataSupport Create(IServiceProvider services)
    {
        var hostEnvironment = services.GetRequiredService<IHostEnvironment>();
        var hostRoot = services.GetRequiredService<HostRoot>();
        var hostFolder = Path.Combine(hostEnvironment.ContentRootPath, hostRoot.Host);
        // one csproj per host folder by design
        var csproj = Directory.EnumerateFiles(hostFolder, "*.csproj").FirstOrDefault()
            ?? throw new InvalidOperationException($"Host project not found. Expected a .csproj in {hostRoot.Host}.");
        var enabled = XDocument.Load(csproj)
            .Descendants("PackageReference")
            .Any(x => x.Attribute("Include")?.Value == METADATA_PACKAGE);
        var path = Path.GetRelativePath(hostEnvironment.ContentRootPath, csproj).Replace('\\', '/');
        return new MetadataSupport(path, enabled);
    }

    public void EnsureEnabled()
    {
        if (!IsEnabled)
            throw new InvalidOperationException(
                $"The application is not metadata-driven: {ProjectPath} has no PackageReference to {METADATA_PACKAGE}. The `meta` commands apply to metadata-driven applications only.");
    }
}

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
*
* Either spelling of a reference answers it. An application built on releases names a package; a
* stand standing beside the repository names the project, precisely so that a change in the layer
* is visible without building one - and a probe that knew only packages called that stand, which
* exists to be driven by this tool, not metadata-driven at all.
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
        var project = XDocument.Load(csproj);
        var enabled = project.Descendants("PackageReference")
                .Any(x => x.Attribute("Include")?.Value == METADATA_PACKAGE)
            || project.Descendants("ProjectReference")
                .Any(x => Path.GetFileNameWithoutExtension(x.Attribute("Include")?.Value ?? String.Empty) == METADATA_PACKAGE);
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

using System;
using System.IO;

using A2v10.Infrastructure;

namespace A2v10.Cli;

/* The web root of the host the CLI runs beside - what IWebHostEnvironment.WebRootPath is on the
 * server, which a console host does not have. 'wwwroot' is ASP.NET's default and the one the
 * hosts keep. The localizer reads the platform's dictionaries from here; the application's own
 * come through IAppCodeProvider.
 */
internal sealed class HostFilesProvider(HostRoot _hostRoot) : IWebHostFilesProvider
{
    public String MapHostingPath(String path) =>
        Path.Combine(_hostRoot.CdPath, _hostRoot.Host, "wwwroot", path);
}

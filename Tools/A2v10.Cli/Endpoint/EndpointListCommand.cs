// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;

namespace A2v10.Cli;

/* The application's own view of its files, not a walk of the disk. The code provider enumerates
 * every REGISTERED module from that module's own root and returns paths relative to it, already
 * prefixed with '$<module>/' for all but the default one - which is exactly how an endpoint is
 * addressed. So there is no module mapping here, and nothing to guard: a folder belonging to no
 * module cannot be reached (the disk walk found those by matching the first path segment and had
 * to drop them again), and neither can anything above the application root.
 *
 * It is the same call DatabaseMetadataProvider.AllElementsMetadata makes, so 'list' and 'deploy'
 * see one set by construction rather than by coincidence. AllElementsMetadata itself is not
 * reusable here: it loads each endpoint and then keeps only the owners of a shape, which drops
 * the operations and the report - right for a deploy, wrong for a list.
 */
internal class EndpointListCommand(IServiceProvider services, String _marker, String _description)
{
    private readonly IAppCodeProvider _codeProvider = services.GetRequiredService<IAppCodeProvider>();

    internal Command Build()
    {
        var cmd = new Command("list", _description);
        cmd.SetAction(r => JsonResult.Try(() => EndpointList()));
        return cmd;
    }

    Task<Object> EndpointList()
    {
        var list = _codeProvider.EnumerateAllFilesRecursive("", _marker)
            // the marker at a module root has no endpoint folder over it, so it names nothing
            .Select(f => Path.GetDirectoryName(f) ?? String.Empty)
            .Where(p => p.Length > 0)
            .Select(p => p.NormalizeSlash())
            // ordinal: the output is read by a program, so the order may not depend on the machine
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult<Object>(list);
    }
}

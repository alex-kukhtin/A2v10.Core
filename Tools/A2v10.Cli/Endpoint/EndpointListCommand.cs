// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using A2v10.Infrastructure;

namespace A2v10.Cli;

internal class EndpointListCommand(IServiceProvider services)
{
    private readonly IHostEnvironment _hostEnvironment = services.GetRequiredService<IHostEnvironment>();
    private readonly AppOptions _appOptions = services.GetRequiredService<IOptions<AppOptions>>().Value;
    internal Command Build()
    {
        var cmd = new Command("list", "List available endpoints");
        cmd.SetAction(r => JsonResult.Try(() => EndpointList()));
        return cmd;
    }

    static IEnumerable<String> FindModelFolders(String root)
    {
        if (File.Exists(Path.Combine(root, "model.json")))
            yield return root;

        IEnumerable<String> subDirs;
        try
        {
            subDirs = Directory.EnumerateDirectories(root);
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }
        catch (DirectoryNotFoundException)
        {
            yield break;
        }

        foreach (String dir in subDirs)
        {
            String name = Path.GetFileName(dir);
            if (String.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(name, "obj", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (String found in FindModelFolders(dir))
                yield return found;
        }
    }
    async Task<Object> EndpointList()
    {
        var rootPath = _hostEnvironment.ContentRootPath;
        var md = _appOptions.Modules?.Select(m =>
            (
                key: $"${m.Key.ToLowerInvariant()}",
                segment: Path.GetFileName(m.Value.Path),
                isMain: m.Value.Default
            )
        ) ?? [];

        String replaceModule(String path)
        {
            var ps = path.Split('/');
            var mx = md.FirstOrDefault(m => m.segment == ps[0]);
            if (mx.segment is null)
                throw new InvalidOperationException($"Folder '{path}' does not belong to any module");
            if (mx.isMain)
                return String.Join('/', ps[1..]);
            else 
                return String.Join("/", [mx.key, ..ps[1..]]);
        }

        String endpointPath(String path)
        {
            var r = Path.GetRelativePath(_hostEnvironment.ContentRootPath, path).NormalizePath();
            return replaceModule(r);
        }

        var list = FindModelFolders(rootPath);
        return list.Select(endpointPath).ToList();
    }
}


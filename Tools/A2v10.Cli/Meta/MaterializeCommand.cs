// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Infrastructure;
using A2v10.Metadata;

namespace A2v10.Cli;

/* Write what the platform would generate for one screen, to edit it from there. One thing per
 * call - the view or the template - because they cost differently to own: see
 * A2v10.Metadata/CLAUDE.md, "Two files in one folder".
 *
 * Never overwrites a file a human owns. The result carries the model.json fragment that makes the
 * platform pick the file up; model.json itself is not touched.
 */
internal sealed class MaterializeCommand(IServiceProvider services)
{
    private readonly EndpointMaterializer _materializer = services.GetRequiredService<EndpointMaterializer>();
    private readonly IAppCodeProvider _codeProvider = services.GetRequiredService<IAppCodeProvider>();

    // no BOM: the file is meant to be opened and saved by an editor, which would add its own
    private static readonly Encoding _utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    internal Command Build()
    {
        var cmd = new Command("materialize",
            "Write the generated view or template of a metadata-driven screen as a file, to edit it by hand. " +
            "Never overwrites. Returns the model.json fragment that points at the file; model.json is not edited.");
        var endpointArg = new Argument<String>("endpoint")
        {
            Description = "Endpoint folder, $prefix for a module (e.g. catalog/agent)"
        };
        var actionArg = new Argument<String>("action")
        {
            Description = "index, browse or edit"
        };
        var whatArg = new Argument<String>("what")
        {
            Description = "view (the .vxaml) or template (the .template.ts and its .d.ts map)"
        };
        cmd.Arguments.Add(endpointArg);
        cmd.Arguments.Add(actionArg);
        cmd.Arguments.Add(whatArg);

        cmd.SetAction(r => JsonResult.Try(() =>
            Materialize(r.GetValue(endpointArg)!, r.GetValue(actionArg)!, r.GetValue(whatArg)!)));
        return cmd;
    }

    private async Task<Object> Materialize(String endpoint, String action, String what)
    {
        MetadataSupport.Create(services).EnsureEnabled();

        if (!Enum.TryParse<MaterializeWhat>(what, ignoreCase: true, out var kind))
            throw new InvalidOperationException($"'{what}': write 'view' or 'template'");

        var result = await _materializer.MaterializeAsync(endpoint, action, kind);

        // full paths first, and every refusal before the first write: a call writes all or nothing
        var files = result.Files.Select(f => (File: f, FullPath: FullPath(f.Path))).ToList();
        var taken = files.Where(f => !f.File.Regenerated && File.Exists(f.FullPath)).Select(f => f.File.Path).ToList();
        if (taken.Count > 0)
            throw new InvalidOperationException(
                $"Already there, not overwritten: {String.Join(", ", taken)}. Delete it to materialize again.");

        foreach (var (file, fullPath) in files)
            await File.WriteAllTextAsync(fullPath, file.Text, _utf8);

        return new Dictionary<String, Object>()
        {
            ["endpoint"] = endpoint.NormalizePath(),
            ["action"] = action.ToLowerInvariant(),
            ["what"] = kind.ToString().ToLowerInvariant(),
            ["files"] = files.Select(f => f.File.Path).ToList(),
            ["modelJson"] = result.ModelJson
        };
    }

    private String FullPath(String relative)
    {
        var dir = Path.GetDirectoryName(relative)?.NormalizePath() ?? String.Empty;
        var full = _codeProvider.GetMainModuleFullPath(dir, Path.GetFileName(relative));
        if (String.IsNullOrEmpty(full))
            throw new InvalidOperationException($"'{relative}' is not on a file system module; nothing to write to");
        return full;
    }
}

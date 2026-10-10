// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Diagnostics;

using A2v10.Infrastructure;
using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The beam over the generated TypeScript: every template and every .d.ts map the platform would
 * materialize for TestApp is written into one folder, laid out as an application is (the
 * platform's types under @types, the application's tsconfig), and tsc is asked. Nothing reads the
 * types at run time - the browser runs the same text with them erased - so until this, a map that
 * promised a member the template could not import, or an import of a name the map did not export,
 * was seen by nobody. See CLAUDE.md, "Validation": generated output is checked only where something
 * reads it back; this is the reader.
 *
 * tsc comes from Tests/TestsMetadata/ts (package.json; 'npm ci' there once). Not found is a failure
 * and not a skip: a beam that is not there must not read as green.
 */
public class TsBeamTests
{
    // bin/<Configuration>/<tfm> -> the project folder
    private static String ProjectDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../.."));

    private static String AssetsDir =>
        Path.GetFullPath(Path.Combine(ProjectDir, "../../Platform/A2v10.App.Assets2026/Application"));

    [Fact]
    public async Task Every_materialized_template_compiles()
    {
        var tsc = Path.Combine(ProjectDir, "ts", "node_modules", "typescript", "bin", "tsc");
        Assert.True(File.Exists(tsc), $"tsc is not installed: run 'npm ci' in {Path.Combine(ProjectDir, "ts")}");

        var dir = Path.Combine(AppContext.BaseDirectory, "tsbeam");
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(Path.Combine(dir, "@types"));
        File.Copy(Path.Combine(AssetsDir, "@types", "platform.d.ts"), Path.Combine(dir, "@types", "platform.d.ts"));
        File.Copy(Path.Combine(AssetsDir, "tsconfig.json"), Path.Combine(dir, "tsconfig.json"));

        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var materializer = new EndpointMaterializer(provider);
        var written = new List<String>();
        foreach (var file in TestHost.GetService<IAppCodeProvider>().EnumerateAllFilesRecursive("", "metadata.json"))
        {
            var folder = Path.GetDirectoryName(file)!.NormalizeSlash();
            var (schema, table) = DatabaseMetadataProvider.ParsePath(folder);
            // the shapes that render: a set or the numbering registry has no screen and no template
            if (await provider.GetEndpointAsync(null, schema, table) is not NormalEndpointMetadata { Declaration.BakedForms.Count: > 0 })
                continue;
            // browse runs the index template (EndpointMaterializer refuses it by design)
            foreach (var action in new[] { "index", "edit" })
            {
                Materialization files;
                try
                {
                    files = await materializer.MaterializeAsync(folder, action, MaterializeWhat.Template);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"{folder} {action}: {ex.Message}", ex);
                }
                foreach (var f in files.Files)
                {
                    var path = Path.Combine(dir, f.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllTextAsync(path, f.Text);
                    written.Add(f.Path);
                }
            }
        }
        Assert.NotEmpty(written);

        var psi = new ProcessStartInfo("node")
        {
            ArgumentList = { tsc, "-p", dir, "--noEmit", "--pretty", "false" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var tscProcess = Process.Start(psi) ?? throw new InvalidOperationException("node did not start");
        var output = await tscProcess.StandardOutput.ReadToEndAsync() + await tscProcess.StandardError.ReadToEndAsync();
        await tscProcess.WaitForExitAsync();
        Assert.True(tscProcess.ExitCode == 0, $"tsc over {written.Count} files in {dir}:\n{output}");
    }
}

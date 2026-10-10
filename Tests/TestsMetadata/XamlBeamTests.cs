// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Metadata;
using A2v10.Services;
using A2v10.Xaml;

namespace A2v10.Metadata.Tests;

/* The beam over the generated XAML: every screen of every endpoint of TestApp is built by the
 * XamlBuilder the runtime uses, written as text by the writer materialize uses, and read back by
 * the reader the platform loads a hand-written view with - then written once more. Two things must
 * hold: the text is a page the platform can read at all, and reading it changes nothing - what the
 * reader made of the text writes back as the same text. An attribute the writer loses, or spells in
 * a form the reader takes differently, is a screen the author ejects and gets back changed, and
 * nothing read the text until now (CLAUDE.md, "Two files in one folder": generated output is
 * checked only where something reads it back).
 *
 * Every screen, not the three forms: the transactions dialog, the print page and the folder
 * dialogs are generated too and ejected by nobody, so this is their only reader.
 */
public class XamlBeamTests
{
    [Fact]
    public async Task Every_generated_screen_reads_back_as_it_was_written()
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var reader = TestHost.GetService<IXamlPartProvider>();
        // what validate refuses is out of scope: TestApp keeps negative fixtures for the validator's own tests
        var validator = new EndpointValidator(provider, TestHost.GetService<IAppCodeProvider>(), TestHost.Services);
        var platformId = await provider.DeclaredPlatformIdAsync()
            ?? throw new InvalidOperationException("TestApp/app.json declares no platformid");

        // the texts stay on disk beside the test's output, to be looked at when one is refused
        var dir = Path.Combine(AppContext.BaseDirectory, "xamlbeam");
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);

        var screens = 0;
        var findings = new List<String>();
        foreach (var file in TestHost.GetService<IAppCodeProvider>().EnumerateAllFilesRecursive("", "metadata.json"))
        {
            var folder = Path.GetDirectoryName(file)!.NormalizeSlash();
            var (schema, table) = DatabaseMetadataProvider.ParsePath(folder);
            if (await provider.GetEndpointAsync(null, schema, table) is not NormalEndpointMetadata { Declaration.BakedForms.Count: > 0 } endpoint)
                continue;
            if ((await validator.ValidateAsync(folder)).Error != null)
                continue;

            foreach (var (action, url) in ScreensOf(endpoint))
            {
                screens++;
                var descriptor = new BuilderDescriptor()
                {
                    Endpoint = endpoint,
                    PlatformUrl = url,
                    PlatformId = platformId,
                    UseGrants = false
                };
                var where = $"{endpoint.Path} {action}";
                try
                {
                    var built = new XamlBuilder(descriptor).CreateXamlContainer(action);
                    var text = XamlTextBulder.GetXaml(built);
                    var path = Path.Combine(dir, folder, $"{action}.vxaml");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllTextAsync(path, text);
                    var read = reader.GetXamlPartText(text, $"{folder}/{action}.vxaml")
                        ?? throw new InvalidOperationException("the reader returned nothing");
                    if (read.GetType() != built.GetType())
                        throw new InvalidOperationException($"built a {built.GetType().Name}, read back a {read.GetType().Name}");
                    var again = XamlTextBulder.GetXaml(read);
                    if (again != text)
                        throw new InvalidOperationException($"the text changes on a read:\n{FirstDifference(text, again)}");
                }
                catch (Exception ex)
                {
                    findings.Add($"{where}: {ex.Message}");
                }
            }
        }

        Assert.True(screens > 0, "no screen was built");
        Assert.True(findings.Count == 0, $"{findings.Count} of {screens} screens:\n{String.Join("\n", findings)}");
    }

    /* Every screen the endpoint renders, with the url the builder reads for it: the print page takes
     * the blank from the query, the rest take nothing but the action.
     */
    private static IEnumerable<(String Action, IPlatformUrl Url)> ScreensOf(NormalEndpointMetadata endpoint)
    {
        foreach (var action in new[] { "index", "indexpartial", "browse", "edit" })
            yield return (action, endpoint.PlatformUrl(action));
        if (endpoint.Declaration.PostJournals().Any())
            yield return (Constants.Trans.Action, endpoint.PlatformUrl(Constants.Trans.Action));
        if (endpoint.Storage.HasFolders)
        {
            yield return ("browsefolder", endpoint.PlatformUrl("browsefolder"));
            yield return ("editfolder", endpoint.PlatformUrl("editfolder"));
        }
        foreach (var form in endpoint.Declaration.PrintForms)
            yield return (Constants.Print.Action,
                endpoint.PlatformUrl(Constants.Print.Action, $"{Constants.Print.FormQuery}={form.Name}"));
    }

    private static String FirstDifference(String expected, String actual)
    {
        var a = expected.Split('\n');
        var b = actual.Split('\n');
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var left = i < a.Length ? a[i] : "<end>";
            var right = i < b.Length ? b[i] : "<end>";
            if (left != right)
                return $"line {i + 1}\n  written: {left.Trim()}\n  reread:  {right.Trim()}";
        }
        return "(lengths differ only)";
    }
}

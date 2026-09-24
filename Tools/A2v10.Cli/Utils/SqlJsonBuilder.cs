// Copyright © 2023-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

namespace A2v10.Cli;

/* sql.json -> the scripts it names: the port of A2v10.Sql.MSBuild (A2v10.Tools/A2v10.BuildSql,
 * SqlFileBuilder). The build and the deploy write the same file, so the text stays byte for byte
 * as there - the header, the BOM, a line break after every input: a difference would show up in
 * git as a change that is not one.
 */
internal static class SqlJsonBuilder
{
    private sealed record SqlFileItem
    {
        public List<String> InputFiles { get; init; } = [];
        public String? OutputFile { get; init; }
    }

    // the full paths of the files written, in the order sql.json names them; none without sql.json
    public static IReadOnlyList<String> Build(String projectDir)
    {
        var jsonPath = Path.Combine(projectDir, "sql.json");
        if (!File.Exists(jsonPath))
            return [];
        var items = JsonConvert.DeserializeObject<List<SqlFileItem>>(File.ReadAllText(jsonPath))
            ?? throw new InvalidOperationException($"Invalid sql.json: {jsonPath}");
        return [.. items.Select(item => BuildItem(projectDir, item))];
    }

    private static String BuildItem(String projectDir, SqlFileItem item)
    {
        var outputFile = item.OutputFile
            ?? throw new InvalidOperationException($"sql.json in {projectDir}: outputFile is not set");
        var outFilePath = Path.GetFullPath(Path.Combine(projectDir, outputFile));
        Directory.CreateDirectory(Path.GetDirectoryName(outFilePath)!);

        var nl = Environment.NewLine;
        using var sw = new StreamWriter(outFilePath, append: false, new UTF8Encoding(true));
        sw.Write($"/* {outputFile} */{nl}{nl}");
        foreach (var pattern in item.InputFiles)
        {
            foreach (var input in ResolveInputFiles(projectDir, pattern))
            {
                sw.Write(File.ReadAllText(input));
                sw.WriteLine();
            }
        }
        return outFilePath;
    }

    private static IEnumerable<String> ResolveInputFiles(String projectDir, String pattern)
    {
        const String recursiveMarker = "/**/";

        var markerIndex = pattern.IndexOf(recursiveMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            yield return Path.Combine(projectDir, pattern);
            yield break;
        }

        var searchRoot = Path.Combine(projectDir, pattern[..markerIndex]);
        if (!Directory.Exists(searchRoot))
            yield break;

        foreach (var file in Directory.EnumerateFiles(searchRoot, pattern[(markerIndex + recursiveMarker.Length)..], SearchOption.AllDirectories))
            yield return file;
    }
}

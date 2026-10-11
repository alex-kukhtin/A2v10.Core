// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Text.RegularExpressions;

namespace A2v10.Metadata.Tests;

/* A refusal the user reads is SqlExtensions.UserError: 60000, 'UI:' and a key. Written by hand, the form
 * was copied from the neighbouring lines and two broken ones spread - 600000, and a key without 'UI:'
 * (review 2026-10-10, section 8). Read off the sources, because that is where the next one is copied from.
 */
public partial class SqlErrorConventionTests
{
    [GeneratedRegex(@"throw\s+6\d{5,}")]
    private static partial Regex WrongNumber();

    [GeneratedRegex(@"throw\s+\d+\s*,\s*N'@\[")]
    private static partial Regex KeyWithoutUI();

    [Fact]
    public void Every_refusal_of_the_layer_keeps_the_one_form()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Platform/A2v10.Metadata"));
        var found = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((line, ix) => (f, ix, line)))
            .Where(x => WrongNumber().IsMatch(x.line) || KeyWithoutUI().IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(root, x.f)}:{x.ix + 1}: {x.line.Trim()}")
            .ToList();

        Assert.True(found.Count == 0, "write SqlExtensions.UserError(key):\n" + String.Join("\n", found));
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Text.RegularExpressions;

namespace A2v10.Metadata.Tests;

/* The keys this package writes into what it generates are its own to translate, in
 * Localization/metadata.{lang}.txt - except those default.{lang}.txt of A2v10.Web.Assets already has.
 * Both are read from the sources, the files the packages ship.
 */
public partial class LocalizationTests
{
    public static TheoryData<String> Languages => ["uk", "en", "ru", "de", "pl", "es", "bg"];

    // bin/<Configuration>/<tfm> -> the repository root
    static String RepoPath(String path) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..", path));

    static String MetadataFile(String lang) => RepoPath($"Platform/A2v10.Metadata/Localization/metadata.{lang}.txt");
    static String DefaultFile(String lang) => RepoPath($"Platform/A2v10.Web.Assets/wwwroot/localization/default.{lang}.txt");

    static HashSet<String> Keys(String path) => [.. File.ReadLines(path)
        .Where(l => l.StartsWith('@'))
        .Select(l => l[1..l.IndexOf('=')].Trim())];

    /* The same keys in every language: a key missing from one is shown raw to its users only. And none
     * of default's: the localizer reads the folder in no defined order, so a key in both is translated
     * by whichever file comes last.
     */
    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_language_has_the_keys_of_uk_and_none_of_default(String lang)
    {
        var keys = Keys(MetadataFile(lang));
        Assert.Equal(Keys(MetadataFile("uk")).Order(), keys.Order());
        Assert.Empty(keys.Intersect(Keys(DefaultFile(lang))));
    }

    /* A literal key only: one composed from a name ('@[{Model}.Browse]') is the application's, and the
     * one composed from a closed set of the platform is listed from the enum. Comments are cut first -
     * they quote keys as examples.
     */
    [Fact]
    public void Every_key_the_package_writes_is_translated()
    {
        var known = Keys(MetadataFile("uk")).Union(Keys(DefaultFile("uk"))).ToHashSet();

        var literal = Directory.EnumerateFiles(RepoPath("Platform/A2v10.Metadata"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => LiteralKey().Matches(Comment().Replace(File.ReadAllText(f), String.Empty)))
            .Select(m => m.Groups[1].Value);

        IEnumerable<String> set<T>(params String[] extra) where T : struct, Enum =>
            [typeof(T).Name, .. Enum.GetNames<T>().Concat(extra).Select(n => $"{typeof(T).Name}.{n}")];

        var missing = literal
            .Concat(set<StateRole>("All"))
            .Concat(set<AccountType>())
            .Concat(set<NormalBalance>())
            .Distinct()
            .Where(k => !known.Contains(k))
            .Order()
            .ToList();
        Assert.Empty(missing);
    }

    [GeneratedRegex(@"@\[([A-Za-z][\w.]*)\]")]
    private static partial Regex LiteralKey();

    [GeneratedRegex(@"/\*.*?\*/|^\s*//[^\r\n]*", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex Comment();
}

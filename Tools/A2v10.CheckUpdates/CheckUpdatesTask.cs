// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.Build.Utilities;

namespace A2v10.CheckUpdates;

// Warns when NuGet has a newer version of THIS package. All A2v10 packages are published
// together, so this package's version is the generation; bump it with every release.
// Never fails the build, never logs its own failure: offline, proxy, a bad answer — silence.
public class CheckUpdatesTask : Task
{
    const string IndexUrl = "https://api.nuget.org/v3-flatcontainer/a2v10.checkupdates/index.json";
    static readonly TimeSpan CacheTtl = TimeSpan.FromDays(1);
    static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(3);

    // per user, not obj/: survives clean and is shared by every app on the machine
    static string CachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "A2v10", "checkupdates.txt");

    public override bool Execute()
    {
        try
        {
            var current = Normalize(Version.Parse(typeof(CheckUpdatesTask).Assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version));
            if (!TryReadCache(out var latest))
                latest = FetchAndCache();
            if (latest != null && latest > current)
                Log.LogWarning(null, "A2V0001", null, null, 0, 0, 0, 0,
                    $"A2v10 {latest} is available; this application is on {current}. " +
                    "Update the A2v10 skill first (the user's step), then update the platform to this version.");
        }
        catch
        {
            // by design: a version check is never worth a broken or noisy build
        }
        return true;
    }

    static bool TryReadCache(out Version? latest)
    {
        latest = null;
        var file = new FileInfo(CachePath);
        if (!file.Exists || DateTime.UtcNow - file.LastWriteTimeUtc > CacheTtl)
            return false;
        // empty cache = the last fetch failed; don't retry until it expires (offline builds pay the timeout once a day)
        Version.TryParse(File.ReadAllText(file.FullName).Trim(), out latest);
        return true;
    }

    static Version? FetchAndCache()
    {
        Version? latest = null;
        try
        {
            using var http = new HttpClient { Timeout = HttpTimeout };
            var json = http.GetStringAsync(IndexUrl).GetAwaiter().GetResult();
            // "x.y.z" closed by a quote = stable only; a prerelease carries a '-suffix' and doesn't match
            latest = Regex.Matches(json, "\"(\\d+\\.\\d+\\.\\d+)\"").Cast<Match>()
                .Select(m => Version.Parse(m.Groups[1].Value))
                .DefaultIfEmpty().Max();
        }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, latest?.ToString() ?? string.Empty);
        }
        return latest;
    }

    // FileVersion is 4-part (10.1.8668.0), NuGet 3-part; Version ranks "10.1.8668" below "10.1.8668.0"
    static Version Normalize(Version v) => new(v.Major, v.Minor, v.Build);
}

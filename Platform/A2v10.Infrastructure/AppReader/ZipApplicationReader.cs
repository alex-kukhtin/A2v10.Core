// Copyright © 2015-2019 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public class ZipApplicationReader : IApplicationReader
{
    private String FileName { get; }

    public Boolean IsFileSystem => false;

    public ZipApplicationReader(String appPath, String appKey)
    {
        String path = Path.Combine(appPath, appKey ?? String.Empty).ToLowerInvariant();
        FileName = Path.ChangeExtension(path, extension: ".app");
    }

    public String MakeFullPath(String path, String fileName)
    {
        if (fileName.StartsWith(value: "/"))
        {
            path = String.Empty;
            fileName = fileName.Remove(startIndex: 0, count: 1);
        }

        // canonicalize
        return GetCanonicalPath(path, fileName);
    }

    public String GetCanonicalPath(String path, String fileName)
    {
        var sep = new String(Path.DirectorySeparatorChar, count: 1);
        String rootPath = Path.GetFullPath(sep);
        String fullPath = Path.GetFullPath(Path.Combine(sep, path, fileName)).Substring(rootPath.Length);
        return fullPath.Replace(oldChar: '\\', newChar: '/').ToLowerInvariant();
    }

    public async Task<String> ReadTextFileAsync(String path, String fileName)
    {
        using StreamReader reader = ZipEntryReader(path, fileName);
        return await reader.ReadToEndAsync();
    }

    public String ReadTextFile(String path, String fileName)
    {
        using StreamReader reader = ZipEntryReader(path, fileName);
        return reader.ReadToEnd();
    }

    public IEnumerable<String> EnumerateFiles(String path, String searchPattern)
    {
        String searchExtension = searchPattern;
        if (searchExtension.StartsWith(value: "*"))
            searchExtension = searchExtension.Substring(startIndex: 1).ToLowerInvariant();
        using var za = ZipFile.OpenRead(FileName);
        foreach (var e in za.Entries)
        {
            var ePath = Path.GetDirectoryName(e.FullName);
            var extMatch = e.Name.EndsWith(searchExtension);
            if (ePath == path && extMatch)
                yield return e.FullName;
        }
    }

    public Boolean DirectoryExists(String fullPath)
    {
        if (String.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }
        using (var za = ZipFile.OpenRead(FileName))
        {
            foreach (var e in za.Entries)
            {
                var ePath = Path.GetDirectoryName(e.FullName);
                if (ePath == fullPath)
                    return true;
            }
        }
        return false;
    }

    public Boolean FileExists(String fullPath)
    {
        if (fullPath == null)
            return false;
        using var za = ZipFile.OpenRead(FileName);
        var ze = za.GetEntry(fullPath);
        return ze != null;
    }

    public String FileReadAllText(String fullPath)
    {
        if (fullPath == null)
            return null;
        using var za = ZipFile.OpenRead(FileName);
        var ze = za.GetEntry(fullPath);
        using var sr = new StreamReader(ze.Open());
        return sr.ReadToEnd();
    }

    public IEnumerable<String> FileReadAllLines(String fullPath)
    {
        if (fullPath != null)
        {
            using var za = ZipFile.OpenRead(FileName);
            var ze = za.GetEntry(fullPath);
            using var sr = new StreamReader(ze.Open());
            while (!sr.EndOfStream)
            {
                yield return sr.ReadLine();
            }
        }
    }

    public Stream FileStream(String path, String fileName)
    {
        using var za = ZipFile.OpenRead(FileName);
        var ze = GetEntry(za, path, fileName);
        return Deflate(ze);
    }

    public Stream FileStreamFullPathRO(String fullPath)
    {
        using var za = ZipFile.OpenRead(FileName);
        var ze = za.GetEntry(fullPath);
        return Deflate(ze);
    }

    public String CombineRelativePath(String path1, String path2)
    {
        return PathHelpers.CombineRelative(path1, path2);
    }

    public String CombinePath(String path1, String path2, String fileName)
    {
        return Path.Combine(path1, path2, fileName);
    }

    private ZipArchiveEntry GetEntry(ZipArchive archive, String path, String fileName)
    {
        String entry = MakeEntry(path, fileName).ToLowerInvariant();
        return archive.GetEntry(entry);
    }

    private StreamReader ZipEntryReader(String path, String fileName)
    {
        using ZipArchive zipArchive = ZipFile.OpenRead(FileName);
        ZipArchiveEntry zipEntry = GetEntry(zipArchive, path, fileName);
        if (zipEntry == null)
            throw new ArgumentNullException(nameof(path));
        return new StreamReader(zipEntry.Open());
    }

    private String MakeEntry(String path, String entry)
    {
        if (String.IsNullOrEmpty(path))
            return entry.ToLowerInvariant();
        return GetCanonicalPath(path, entry);
    }

    private Stream Deflate(ZipArchiveEntry entry)
    {
        if (entry == null)
            return null;
        var ms = new MemoryStream();
        using (var ds = entry.Open())
        {
            ds.CopyTo(ms);
        }
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}

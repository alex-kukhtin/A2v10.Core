// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.IO;

using System.IO.Compression;

namespace A2v10.Request;

public static class ZipUtils
{
    private const String CONTENT = "content";

    public static Byte[] CompressText(String text)
    {
        using MemoryStream ms = new();
        using ZipArchive archive = new(ms, ZipArchiveMode.Create);

        ZipArchiveEntry entry = archive.CreateEntry(CONTENT, CompressionLevel.Fastest);

        using StreamWriter wr = new(entry.Open());
        wr.Write(text);
        return ms.ToArray();
    }

    public static String DecompressText(Stream stream)
    {
        using ZipArchive archive = new(stream, ZipArchiveMode.Read);

        ZipArchiveEntry? entry = archive.GetEntry(CONTENT) ?? 
            throw new ArgumentException(message: $"Entry {CONTENT} doesn\'t exist in the archive", nameof(stream));

        using StreamReader sr = new(entry.Open());
        return sr.ReadToEnd();
    }
}

// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO.Compression;
using System.IO;

namespace A2v10.Services;

public static class ZipUtils
{
    private const String CONTENT = "content";
    public static Byte[] CompressText(String text)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry(CONTENT, CompressionLevel.Fastest);
            using var wr = new StreamWriter(entry.Open());
            wr.Write(text);
        }
        return ms.ToArray();
    }

    public static String DecompressText(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry(CONTENT)
            ?? throw new FileFormatException("No CONTENT entry");
        using var sr = new StreamReader(entry.Open());
        return sr.ReadToEnd();
    }
}

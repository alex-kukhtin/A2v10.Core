// Copyright © 2015-2019 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public class FileApplicationReader : IApplicationReader
{
    private String AppPath { get; }
    private String AppKey { get; }

    public Boolean IsFileSystem => true;

    public Boolean EmulateBox { get; set; }


    public FileApplicationReader(String appPath, String appKey)
    {
        AppPath = appPath;
        AppKey = appKey;
    }

    public async Task<String> ReadTextFileAsync(String path, String fileName)
    {
        String fullPath = GetFullPath(path, fileName);

        using var tr = new StreamReader(fullPath);
        return await tr.ReadToEndAsync();
    }

    public String ReadTextFile(String path, String fileName)
    {
        String fullPath = GetFullPath(path, fileName);

        using var tr = new StreamReader(fullPath);
        return tr.ReadToEnd();
    }

    public IEnumerable<String> EnumerateFiles(String path, String searchPattern)
    {
        if (String.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));
        var fullPath = GetFullPath(path, String.Empty);
        if (!Directory.Exists(fullPath))
            throw new ArgumentNullException(nameof(path), message: $"Directory {fullPath} doesn't exist");
        return Directory.EnumerateFiles(fullPath, searchPattern);
    }

    public Boolean FileExists(String fullPath)
    {
        return File.Exists(fullPath);
    }

    public Boolean DirectoryExists(String fullPath)
    {
        return Directory.Exists(fullPath);
    }

    public String FileReadAllText(String fullPath)
    {
        return File.ReadAllText(fullPath);
    }

    public Stream FileStreamFullPathRO(String fullPath)
    {
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public String MakeFullPath(String path, String fileName)
    {
        return GetFullPath(path, fileName);
    }        

    public String CombineRelativePath(String path1, String path2)
    {
        return Path.GetFullPath(Path.Combine(path1, path2));
    }

    public String CombinePath(String path1, String path2, String fileName)
    {
        return Path.Combine(path1, path2, fileName);
    }

    private String GetFullPath(String path, String fileName)
    {
        String appKey = AppKey;
        if (fileName.StartsWith(value: "/"))
        {
            path = String.Empty;
            fileName = fileName.Remove(startIndex: 0, count: 1);
        }
        if (appKey != null)
            appKey = "/" + appKey;

        if (path.StartsWith("$"))
        {
            path = path.Replace(oldValue: "$", newValue: "../");
        }

        String fullPath = Path.Combine($"{AppPath}{appKey}", path, fileName);

        if (EmulateBox)
        {
            var ext = Path.GetExtension(fullPath);
            if (!String.IsNullOrEmpty(ext))
            {
                var boxPath = $"{fullPath.Substring(startIndex: 0, fullPath.Length - ext.Length)}.box{ext}";
                var boxFullPath = Path.GetFullPath(boxPath);
                if (File.Exists(boxFullPath))
                    return boxFullPath;
            }
        }

        return Path.GetFullPath(fullPath);
    }
}

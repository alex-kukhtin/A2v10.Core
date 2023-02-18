﻿// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public class ClrApplicationReader : IApplicationReader
{
    private readonly IAppContainer _container;
    private readonly String _appPath;

    public ClrApplicationReader(String appPath, String key)
    {
        if (_container != null)
            return;
        var (assembly, type) = ClrHelpers.ParseClrType(key);
        var ass = Assembly.Load(assembly);
        if (ass == null)
            throw new FileNotFoundException(assembly);
        var inst = ass.CreateInstance(type);
        if (inst == null)
            throw new InvalidOperationException(message: "Invalid type name");
        if (inst is IAppContainer appCont)
        {
            _appPath = appPath;
            _container = appCont;
        }
        else
            throw new InvalidOperationException("Invalid CLR type");
    }

    public Boolean IsFileSystem => false;

    public String CombineRelativePath(string path1, string path2)
    {
        return PathHelpers.CombineRelative(path1, path2);
    }

    public String CombinePath(String path1, String path2, String fileName)
    {
        return Path.Combine(path1, path2, fileName);
    }

    public Boolean DirectoryExists(String fullPath)
    {
        return _container.EnumerateFiles(fullPath, pattern: "").Any();
    }

    public IEnumerable<String> EnumerateFiles(String path, String searchPattern)
    {
        if (String.IsNullOrEmpty(path))
            return Enumerable.Empty<String>();
        if (searchPattern.StartsWith(value: "*"))
            searchPattern = searchPattern.Substring(startIndex: 1);
        return _container.EnumerateFiles(path, searchPattern);
    }

    public bool FileExists(string fullPath)
    {
        return _container.FileExists(fullPath);
    }

    public String? FileReadAllText(String fullPath)
    {
        return _container.GetText(fullPath);
    }

    public Stream FileStreamFullPathRO(String fullPath)
    {
        return _container.GetStream(fullPath);
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

    public String ReadTextFile(String path, String fileName)
    {
        var fullPath = MakeFullPath(path, fileName);
        return _container.GetText(fullPath);
    }

    public Task<String> ReadTextFileAsync(String path, String fileName)
    {
        return Task.FromResult(ReadTextFile(path, fileName));
    }

    public String GetCanonicalPath(String path, String fileName)
    {
        var sep = new String(Path.DirectorySeparatorChar, count: 1);
        String rootPath = Path.GetFullPath(sep);
        String fullPath = Path.GetFullPath(Path.Combine(sep, path, fileName)).Substring(rootPath.Length);
        return fullPath.Replace(oldChar: '\\', newChar: '/').ToLowerInvariant();
    }
}

﻿// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;

namespace A2v10.Services;

public class InternalAppCodeProviderFile : IAppCodeProviderImpl
{
	private String AppPath { get; }

	public Boolean IsFileSystem => true;

	public InternalAppCodeProviderFile(String path)
	{
		AppPath = path;
	}

	public String MakeFullPath(String path, String fileName, Boolean admin)
	{
		if (path.StartsWith("$"))
		{
			int ix = path.IndexOf("/");
			path = path[(ix + 1)..];
		}

        if (fileName.StartsWith('/'))
		{
			path = String.Empty;
			fileName = fileName.Remove(0, 1);
		}
		String fullPath = Path.Combine($"{AppPath}", path, fileName);

		return Path.GetFullPath(fullPath);
	}

    public String? MakeFullPathCheck(String path, String fileName)
	{
		var fullPath = MakeFullPath(path, fileName, false);
		if (File.Exists(fullPath))
			return fullPath;
		return null;
	}

    public async Task<String?> ReadTextFileAsync(String path, String fileName, Boolean admin)
	{
		String fullPath = MakeFullPath(path, fileName, admin);

		if (!File.Exists(fullPath))
			return null;

		using var tr = new StreamReader(fullPath);
		return await tr.ReadToEndAsync();
	}

	public String? ReadTextFile(String path, String fileName, Boolean admin)
	{
		String fullPath = MakeFullPath(path, fileName, admin);

		if (!File.Exists(fullPath))
			return null;

		using var tr = new StreamReader(fullPath);
		return tr.ReadToEnd();
    }

    public Boolean IsFileExists(String path, String fileName)
	{
		var fullPath = MakeFullPath(path, fileName, false);
        return File.Exists(fullPath);
    }

    public Boolean FileExists(String fullPath)
	{
		return File.Exists(fullPath);
	}

	public Boolean DirectoryExists(String fullPath)
	{
		return Directory.Exists(fullPath);
	}

    public Stream FileStreamRO(String path)
	{
		var fullPath = MakeFullPath(path, "", false);
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
    public IEnumerable<String> EnumerateFiles(String? path, String searchPattern)
	{
		if (String.IsNullOrEmpty(path))
			yield break;
		var fullPath = MakeFullPath(path, String.Empty, false);
		if (!Directory.Exists(fullPath))
			yield break;
		foreach (var f in Directory.EnumerateFiles(fullPath, searchPattern))
		{
			var relPath = Path.GetRelativePath(AppPath, f);
			yield return relPath;
		}
	}
	public String ReplaceFileName(String baseFullName, String relativeName)
    {
        String dir = Path.GetDirectoryName(baseFullName) ?? String.Empty;
        return Path.GetFullPath(Path.Combine(dir, relativeName));
	}
}



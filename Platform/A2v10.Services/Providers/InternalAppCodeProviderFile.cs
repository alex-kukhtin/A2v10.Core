// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;

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

    public Boolean IsFileExists(String path, String fileName)
	{
		var fullPath = MakeFullPath(path, fileName, false);
        return File.Exists(fullPath);
    }

    public Stream? FileStreamRO(String path)
	{
		var fullPath = MakeFullPath(path, "", false);
		if (!File.Exists(fullPath))
			return null;
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
    public IEnumerable<String> EnumerateFiles(String path, String searchPattern)
	{
		var fullPath = MakeFullPath(path, String.Empty, false);
		if (!Directory.Exists(fullPath))
			yield break;
		foreach (var f in Directory.EnumerateFiles(fullPath, searchPattern))
		{
			var relPath = Path.GetRelativePath(AppPath, f);
			yield return relPath;
		}
	}
}



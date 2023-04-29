// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;

namespace A2v10.Services;

public class InternalAppCodeProviderFile : IAppCodeProviderImpl
{
	private String AppPath { get; }

	public Boolean IsFileSystem => true;
	public Boolean IsLicensed => false;
	public Guid? ModuleId => null;	

	public InternalAppCodeProviderFile(String path)
	{
		AppPath = path;
	}

	public String NormalizePath(String path)
	{
        if (path.StartsWith("$"))
        {
            int ix = path.IndexOf("/");
            path = path[(ix + 1)..];
        }

        String fullPath = Path.Combine($"{AppPath}", path);

        return Path.GetFullPath(fullPath);
    }

   
    public Boolean IsFileExists(String path)
	{
		var fullPath = NormalizePath(path);
        return File.Exists(fullPath);
    }

    public Stream? FileStreamRO(String path)
	{
		var fullPath = NormalizePath(path);
		if (!File.Exists(fullPath))
			return null;
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
    public IEnumerable<String> EnumerateFiles(String path, String searchPattern)
	{
		var fullPath = NormalizePath(path);
		if (!Directory.Exists(fullPath))
			yield break;
		foreach (var f in Directory.EnumerateFiles(fullPath, searchPattern))
		{
			var relPath = Path.GetRelativePath(AppPath, f);
			yield return relPath;
		}
	}
}



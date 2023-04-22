// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Text;

using A2v10.App.Abstractions;

namespace A2v10.Services;

public class InternalAppCodeProviderClr : IAppCodeProviderImpl
{

	private readonly IAppContainer _appContainer;

	public Boolean IsFileSystem => false;


	public InternalAppCodeProviderClr(IAppContainer appContainer)
	{
		_appContainer = appContainer;
    }

    public static String MakeFullPath(String path)
	{
        if (path.StartsWith("$"))
        {
            int ix = path.IndexOf("/");
            path =  path[(ix + 1)..];
        }
		return path.NormalizeSlash();
    }

    public String MakeFullPath(String path, String fileName, Boolean admin)
	{
		if (path.StartsWith("$"))
		{
			int ix = path.IndexOf("/");
			path = path[(ix+1).. ];
		}
		return Path.GetRelativePath(".", Path.Combine(path, fileName)).NormalizeSlash();
	}

    public Boolean IsFileExists(String path, String fileName)
	{
        var fullPath = MakeFullPath(path, fileName, false);
        return FileExists(fullPath);
	}

	public Boolean FileExists(String fullPath)
	{
		return !String.IsNullOrEmpty(_appContainer.GetText(fullPath));
	}

	public Boolean DirectoryExists(String fullPath)
	{
		return _appContainer.EnumerateFiles(fullPath, "").Any();
	}

    public Stream? FileStreamRO(String path)
	{
		var fullPath = MakeFullPath(path);
		if (!File.Exists(fullPath))
			return null;
        return new MemoryStream(Encoding.UTF8.GetBytes(_appContainer.GetText(fullPath) ?? String.Empty));
    }

    public IEnumerable<String> EnumerateFiles(String? path, String searchPattern)
	{
		if (String.IsNullOrEmpty(path))
			return Enumerable.Empty<String>();
		if (searchPattern.StartsWith('*'))
			searchPattern = searchPattern[1..];
		return _appContainer.EnumerateFiles(path, searchPattern);
	}
}



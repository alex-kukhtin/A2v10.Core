// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;
using System.Text;

namespace A2v10.Services;

public class InternalAppCodeProviderClr : IAppCodeProvider
{

	private readonly IAppContainer _appContainer;

	public Boolean IsFileSystem => false;


	public InternalAppCodeProviderClr(IAppContainer appContainer)
	{
		_appContainer = appContainer;
    }

	public String MakeFullPath(String path, String fileName, Boolean admin)
	{
		if (path.StartsWith("$"))
			path = path.Replace("$", "../");
		return Path.GetRelativePath(".", Path.Combine(path, fileName)).NormalizeSlash();
	}

    public String? MakeFullPathCheck(String path, String fileName)
	{
		var fullPath = MakeFullPath(path, fileName, false);
		if (FileExists(fullPath))
			return fullPath;
		return null;
	}


    public Task<String?> ReadTextFileAsync(String path, String fileName, Boolean admin)
	{
		String fullPath = MakeFullPath(path, fileName, admin);
		return Task.FromResult<String?>(_appContainer.GetText(fullPath));
	}

	public String? ReadTextFile(String path, String fileName, Boolean admin)
	{
		String fullPath = MakeFullPath(path, fileName, admin);
		return _appContainer.GetText(fullPath);
	}

	public Boolean FileExists(String fullPath)
	{
		return !String.IsNullOrEmpty(_appContainer.GetText(fullPath));
	}

	public Boolean DirectoryExists(String fullPath)
	{
		return _appContainer.EnumerateFiles(fullPath, "").Any();
	}

	public Stream FileStreamFullPathRO(String fullPath)
	{
		return new MemoryStream(Encoding.UTF8.GetBytes(_appContainer.GetText(fullPath) ?? String.Empty));
	}

	public IEnumerable<String> EnumerateFiles(String? path, String searchPattern, Boolean admin)
	{
		if (String.IsNullOrEmpty(path))
			return Enumerable.Empty<String>();
		if (searchPattern.StartsWith('*'))
			searchPattern = searchPattern[1..];
		return _appContainer.EnumerateFiles(path, searchPattern);
	}

	public String ReplaceFileName(String baseFullName, String relativeName)
    {
        String dir = Path.GetDirectoryName(baseFullName) ?? String.Empty;
		return Path.GetRelativePath(".", Path.Combine(dir, relativeName)).NormalizeSlash();
	}
}



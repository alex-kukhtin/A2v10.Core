// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;
using System.Text;

using Microsoft.Extensions.Options;


namespace A2v10.Services;

public class ClrCodeProvider : IAppCodeProvider
{

	private readonly IAppContainer _appContainer;

	private String AppPath { get; }

	public Boolean IsFileSystem => false;

	private readonly String _appKey;

	public ClrCodeProvider(IOptions<AppOptions> appOptions,
		IAppProvider appProvider)
	{
		AppPath = appOptions.Value.Path;
		_appKey = appOptions.Value.AppName;
		_appContainer = appProvider.Container;
	}

	String GetAppKey(Boolean admin)
	{
		return admin ? "Admin" : _appKey;
	}

	public String MakeFullPath(String path, String fileName, Boolean admin)
	{
		if (path.StartsWith("$"))
			path = path.Replace("$", "../");
		return Path.GetRelativePath(".", Path.Combine(path, fileName)).NormalizeSlash();
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

	String GetFullPath(String path, String fileName, Boolean admin)
	{
		String appKey = GetAppKey(admin);
		if (fileName.StartsWith("/"))
		{
			path = String.Empty;
			fileName = fileName.Remove(0, 1);
		}
		if (appKey != null)
			appKey = "/" + appKey;
		String fullPath = Path.Combine($"{AppPath}{appKey}", path, fileName);

		return Path.GetFullPath(fullPath);
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

	public String GetExtension(String fullName)
	{
		return Path.GetExtension(fullName);
	}
		
	public String ChangeExtension(String fullName, String extension)
	{
		return Path.ChangeExtension(fullName, extension);
	}
}



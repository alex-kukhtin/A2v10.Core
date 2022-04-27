// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

using A2v10.Infrastructure;
using Microsoft.Extensions.Options;

namespace A2v10.Platform.Web;
public class FileSystemCodeProvider : IAppCodeProvider
{

	private readonly IWebHostEnvironment _webHost;

	private String AppPath { get; }

	public Boolean IsFileSystem => true;

	private readonly String _appKey;

	public FileSystemCodeProvider(IWebHostEnvironment webHost, IOptions<AppOptions> appOptions)
	{
		_webHost = webHost;
		AppPath = appOptions.Value.Path;
		_appKey = appOptions.Value.AppName;
	}

	String GetAppKey(Boolean admin)
	{
		return admin ? "Admin" : _appKey;
	}

	public String MakeFullPath(String path, String fileName, Boolean admin)
	{
		String appKey = GetAppKey(admin);
		if (fileName.StartsWith('/'))
		{
			path = String.Empty;
			fileName = fileName.Remove(0, 1);
		}
		if (appKey != null)
			appKey = "/" + appKey;
		String fullPath = Path.Combine($"{AppPath}{appKey}", path, fileName);

		return Path.GetFullPath(fullPath);
	}

	public async Task<String?> ReadTextFileAsync(String path, String fileName, Boolean admin)
	{
		String fullPath = MakeFullPath(path, fileName, admin);

		if (!File.Exists(fullPath))
			return null;

		using var tr = new StreamReader(fullPath);
		return await tr.ReadToEndAsync();
	}

	public Boolean FileExists(String fullPath)
	{
		return File.Exists(fullPath);
	}

	public Boolean DirectoryExists(String fullPath)
	{
		return Directory.Exists(fullPath);
	}

	public Stream FileStreamFullPathRO(String fullPath)
	{
		return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
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
		var fullPath = GetFullPath(path, String.Empty, admin);
		if (!Directory.Exists(fullPath))
			return Enumerable.Empty<String>();
		return Directory.EnumerateFiles(fullPath, searchPattern);
	}


	public String ReplaceFileName(String baseFullName, String relativeName)
    {
        String dir = Path.GetDirectoryName(baseFullName) ?? String.Empty;
        return Path.GetFullPath(Path.Combine(dir, relativeName));
	}

	public String GetExtension(String fullName)
	{
		return Path.GetExtension(fullName);
	}
		
	public String ChangeExtension(String fullName, String extension)
	{
		return Path.ChangeExtension(fullName, extension);
	}

	public String MapHostingPath(String path)
	{
		return Path.Combine(_webHost.WebRootPath, path);
	}
}



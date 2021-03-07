// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;

using A2v10.Infrastructure;

namespace A2v10.Core.Web.Mvc
{
	public class FileSystemCodeProvider : IAppCodeProvider
	{

		private readonly IWebHostEnvironment _webHost;

		private String AppPath { get; }
		private String AppKey { get; }

		public Boolean IsFileSystem => true;

		public FileSystemCodeProvider(IWebHostEnvironment webHost, String appPath, String appKey)
		{
			_webHost = webHost;
			AppPath = appPath;
			AppKey = appKey;
		}

		public String MakeFullPath(String path, String fileName)
		{
			String appKey = AppKey;
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

		public async Task<String> ReadTextFileAsync(String path, String fileName)
		{
			String fullPath = MakeFullPath(path, fileName);

			if (!File.Exists(fullPath))
				return null;

			using var tr = new StreamReader(fullPath);
			return await tr.ReadToEndAsync();
		}

		public String ReadTextFile(String path, String fileName)
		{
			String fullPath = MakeFullPath(path, fileName);
			if (!File.Exists(fullPath))
				return null;
			using var tr = new StreamReader(fullPath);
			return tr.ReadToEnd();
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

		public String FileReadAllText(String fullPath)
		{
			return File.ReadAllText(fullPath);
		}


		String GetFullPath(String path, String fileName)
		{
			String appKey = AppKey;
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

		public IEnumerable<String> FileReadAllLines(String fullPath)
		{
			if (String.IsNullOrEmpty(fullPath))
				return Enumerable.Empty<String>();
			return File.ReadAllLines(fullPath);
		}

		public IEnumerable<String> EnumerateFiles(String path, String searchPattern)
		{
			if (String.IsNullOrEmpty(path))
				return Enumerable.Empty<String>();
			var fullPath = GetFullPath(path, String.Empty);
			if (!Directory.Exists(fullPath))
				return Enumerable.Empty<String>();
			return Directory.EnumerateFiles(fullPath, searchPattern);
		}


		public String ReplaceFileName(String baseFullName, String relativeName)
		{
			String dir = Path.GetDirectoryName(baseFullName);
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
}


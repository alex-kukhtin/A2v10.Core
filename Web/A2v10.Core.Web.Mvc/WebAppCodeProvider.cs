// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using A2v10.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace A2v10.Core.Web.Mvc
{
	public class WebAppCodeProvider : IAppCodeProvider
	{

		private readonly IConfiguration _appSection;
		public WebAppCodeProvider(IConfiguration config)
		{
			_appSection = config.GetSection("application");
		}

		private String AppPath => _appSection.GetValue<String>("path");
		private String AppKey => _appSection.GetValue<String>("key");

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
	}
}

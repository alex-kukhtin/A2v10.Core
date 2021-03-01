// Copyright © 2015-2020 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;

using A2v10.Infrastructure;
using Newtonsoft.Json;

namespace A2v10.Core.Web.Mvc
{
	// TODO:
	public class RedirectModule
	{
		private readonly IDictionary<String, String> _redirect;
		private readonly FileSystemWatcher _redirectWatcher;
		private Boolean _loaded;

		public RedirectModule()
		{
			Start();
		}

		public void Start()
		{
			CreateWatcher();
			Read();
		}

		public void Read()
		{
			if (_loaded)
				return;
			/* TODO
			String redJson = _host.ApplicationReader.ReadTextFile(String.Empty, "redirect.json");
			if (redJson != null)
				_redirect = JsonConvert.DeserializeObject<Dictionary<String, String>>(redJson);
			_loaded = true;
			*/
		}

		public void CreateWatcher()
		{
			/* TODO
			if (_host.IsDebugConfiguration && _redirectWatcher == null && _host.ApplicationReader.IsFileSystem)
			{
				String redFilePath = _host.ApplicationReader.MakeFullPath(String.Empty, "redirect.json");
				var dirName = Path.GetDirectoryName(redFilePath);
				if (!Directory.Exists(dirName))
					return;
				// FileName can be in 8.3 format!
				_redirectWatcher = new FileSystemWatcher(Path.GetDirectoryName(redFilePath), "*.*")
				{
					NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
				};
				_redirectWatcher.Changed += (sender, e) =>
				{
					_loaded = false;
				};
				_redirectWatcher.EnableRaisingEvents = true;
			}
			*/
		}

		public String Redirect(String path)
		{
			Read();
			if (_redirect == null)
				return path;
			if (_redirect.TryGetValue(path, out String outPath))
				return outPath;
			return path;
		}
	}
}

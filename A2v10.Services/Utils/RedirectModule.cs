// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

namespace A2v10.Services
{
	public class RedirectModule
	{
		private readonly String _path;
		private readonly ConcurrentDictionary<String, String> _dict = new(StringComparer.OrdinalIgnoreCase);

		public RedirectModule(String path, Boolean watch)
		{
			_path = path;
			Read();
			if (watch)
				CreateWatcher();
		}

		private void Read()
		{
			var jsonText = File.ReadAllText(_path);
			if (String.IsNullOrEmpty(jsonText))
				return;
			var d = JsonConvert.DeserializeObject<Dictionary<String, String>>(jsonText);
			foreach (var (k, v) in d)
			{
				_dict.TryAdd(k, v);
			}
		}

		private FileSystemWatcher CreateWatcher()
		{
			// redirect file name in 8.3 format!
			var redirectWatcher = new FileSystemWatcher(Path.GetDirectoryName(_path), "*.*")
			{
				NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
			};
			redirectWatcher.Changed += (sender, e) =>
			{
				_dict.Clear();
				Read();
			};
			redirectWatcher.EnableRaisingEvents = true;
			return redirectWatcher;
		}

		public String Redirect(String path)
		{
			if (_dict.TryGetValue(path, out String outPath))
				return outPath;
			return path;
		}
	}
}

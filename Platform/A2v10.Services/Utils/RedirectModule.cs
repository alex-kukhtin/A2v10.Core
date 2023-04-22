// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System.Collections.Concurrent;
using System.IO;

using Newtonsoft.Json;

namespace A2v10.Services;
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
		if (d == null)
			return;
		foreach (var (k, v) in d)
		{
			_dict.TryAdd(k, v);
		}
	}

	private FileSystemWatcher CreateWatcher()
	{
		// redirect file name in 8.3 format!
		var dirname = Path.GetDirectoryName(_path) 
			?? throw new InvalidProgramException("Directory is null");
        var redirectWatcher = new FileSystemWatcher(dirname, "*.*")
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
		if (_dict.TryGetValue(path, out String? outPath))
			return outPath;
		return path;
	}
}


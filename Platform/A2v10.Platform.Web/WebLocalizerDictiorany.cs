// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web
{

	class LocaleMapItem
	{
		public ConcurrentDictionary<String, String> Map { get; } = new();

		public LocaleMapItem(Action<ConcurrentDictionary<String, String>> action)
		{
			action(Map);
		}
	}

	internal record LocalePath(String Path, Boolean IsFileSystem);

	public class WebLocalizerDictiorany : ILocalizerDictiorany
	{
		private readonly ConcurrentDictionary<String, LocaleMapItem> _maps = new();

		FileSystemWatcher _watcher_system = null;
		FileSystemWatcher _watcher_app = null;



		private readonly IAppCodeProvider _appCodeProvider;
		private readonly Boolean _watch;

		public WebLocalizerDictiorany(IAppCodeProvider appCodeProvider, IAppConfiguration configuration)
		{
			_appCodeProvider = appCodeProvider;
			_watch = configuration.Watch;
		}

		static IEnumerable<String> ReadLines(IAppCodeProvider codeProvider, String path)
		{
			using var stream = codeProvider.FileStreamFullPathRO(path);
			using var rdr = new StreamReader(stream);
			while (!rdr.EndOfStream)
			{
				var s = rdr.ReadLine();
				if (String.IsNullOrEmpty(s) || s.StartsWith(";"))
					continue;
				yield return s;
			}
		}

		public IDictionary<String, String> GetLocalizerDictionary(String locale)
		{
			var localMap = GetCurrentMap(locale, (map) =>
			{
				foreach (var localePath in GetLocalizerFilePath(locale))
				{
					foreach (var line in ReadLines(_appCodeProvider, localePath.Path))
					{
						Int32 pos = line.IndexOf('=');
						if (pos != -1)
						{
							var key = line.Substring(0, pos);
							var val = line[(pos + 1)..];
							map.AddOrUpdate(key, val, (k, oldVal) => val);
						}
						else
							throw new InvalidDataException($"Invalid dictionary string '{line}'");
					}
				}
			});

			return localMap.Map;
		}

		IEnumerable<LocalePath> GetLocalizerFilePath(String locale)
		{
			// locale may be "uk-UA"
			var dirPath = _appCodeProvider.MapHostingPath("localization");

			var appPath = _appCodeProvider.MakeFullPath("_localization", String.Empty, admin:false);

			if (!Directory.Exists(dirPath))
				dirPath = null;

			if (!_appCodeProvider.DirectoryExists(appPath))
				appPath = null;

			if (dirPath == null && appPath == null)
				yield break;

			CreateWatchers(dirPath, _appCodeProvider.IsFileSystem ? appPath : null);
			if (dirPath != null)
			{
				foreach (var s in Directory.EnumerateFiles(dirPath, $"*.{locale}.txt"))
					yield return new LocalePath(s, true);
			}

			foreach (var s in _appCodeProvider.EnumerateFiles(appPath, $"*.{locale}.txt", false))
				yield return new LocalePath(s, false);

			// simple locale: uk
			if (locale.Length > 2)
			{
				locale = locale.Substring(0, 2);
				if (dirPath != null)
				{
					// System.IO!
					foreach (var s in Directory.EnumerateFiles(dirPath, $"*.{locale}.txt"))
						yield return new LocalePath(s, true);
				}
				foreach (var s in _appCodeProvider.EnumerateFiles(appPath, $"*.{locale}.txt", false))
					yield return new LocalePath(s, false);
			}
		}

		LocaleMapItem GetCurrentMap(String locale, Action<ConcurrentDictionary<String, String>> action)
		{
			return _maps.GetOrAdd(locale, (key) => new LocaleMapItem(action));
		}

		void CreateWatchers(String dirPath, String appPath)
		{
			if (!_watch)
				return;
			if (!String.IsNullOrEmpty(dirPath))
			{
				// FileName can be in 8.3 format!
				_watcher_system = new FileSystemWatcher(dirPath, "*.*")
				{
					NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
				};
				_watcher_system.Changed += Watcher_Changed;
				_watcher_system.Created += Watcher_Changed;
				_watcher_system.Deleted += Watcher_Changed;
				_watcher_system.EnableRaisingEvents = true;
			}

			if (!String.IsNullOrEmpty(appPath))
			{
				// FileName can be in 8.3 format!
				_watcher_app = new FileSystemWatcher(appPath, "*.*")
				{
					NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
				};
				_watcher_app.Changed += Watcher_Changed;
				_watcher_app.Created += Watcher_Changed;
				_watcher_app.Deleted += Watcher_Changed;
				_watcher_app.EnableRaisingEvents = true;
			}
		}

		private void Watcher_Changed(Object sender, FileSystemEventArgs e)
		{
			_maps.Clear();
		}
	}
}

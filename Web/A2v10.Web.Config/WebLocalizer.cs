// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Web.Config
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


	internal class WebDictionary
	{
		private readonly ConcurrentDictionary<String, LocaleMapItem> _maps = new();

		FileSystemWatcher _watcher_system = null;
		FileSystemWatcher _watcher_app = null;

		private readonly IAppCodeProvider _appCodeProvider;

		public WebDictionary(IAppCodeProvider appCodeProvider)
		{
			_appCodeProvider = appCodeProvider;
		}

		public IDictionary<String, String> GetLocalizerDictionary(String locale)
		{
			var localMap = GetCurrentMap(locale, (map) =>
			{
				foreach (var localePath in GetLocalizerFilePath(locale))
				{
					foreach (var line in _appCodeProvider.FileReadAllLines(localePath.Path))
					{
						if (String.IsNullOrWhiteSpace(line))
							continue;
						if (line.StartsWith(";"))
							continue;
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
			// locale may be "uk_UA"
			var dirPath = _appCodeProvider.MapHostingPath("localization");

			var appPath = _appCodeProvider.MakeFullPath("_localization", String.Empty);

			if (!Directory.Exists(dirPath))
				dirPath = null;

			if (!_appCodeProvider.DirectoryExists(appPath))
				appPath = null;

			if (dirPath == null && appPath == null)
				yield break;

			//CreateWatchers(host, dirPath, _appCodeProvider.IsFileSystem ? appPath : null);
			if (dirPath != null)
			{
				foreach (var s in Directory.EnumerateFiles(dirPath, $"*.{locale}.txt"))
					yield return new LocalePath(s, true);
			}

			foreach (var s in _appCodeProvider.EnumerateFiles(appPath, $"*.{locale}.txt"))
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
				foreach (var s in _appCodeProvider.EnumerateFiles(appPath, $"*.{locale}.txt"))
					yield return new LocalePath(s, false);
			}
		}

		LocaleMapItem GetCurrentMap(String locale, Action<ConcurrentDictionary<String, String>> action)
		{
			return _maps.GetOrAdd(locale, (key) => new LocaleMapItem(action));
		}

		void CreateWatchers(IApplicationHost host, String dirPath, String appPath)
		{
			if (_watcher_system != null)
				return;
			if (!host.IsDebugConfiguration || host.IsProductionEnvironment)
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

	public class WebLocalizer : BaseLocalizer, IDataLocalizer
	{
		private readonly WebDictionary _webDictionary;

		private readonly IAppCodeProvider _appCodeProvider;

		public WebLocalizer(IAppCodeProvider appCodeProvider, String defaultLocale)
			: base(defaultLocale)
		{
			_appCodeProvider = appCodeProvider;
			_webDictionary = new WebDictionary(_appCodeProvider);
		}

		#region IDataLocalizer
		public String Localize(String content)
		{
			return Localize(null, content, true);
		}
		#endregion

		protected override IDictionary<String, String> GetLocalizerDictionary(String locale)
		{
			return _webDictionary.GetLocalizerDictionary(locale);
		}
	}
}

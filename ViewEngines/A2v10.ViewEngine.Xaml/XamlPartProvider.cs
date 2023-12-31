// Copyright © 2022-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.Options;

using A2v10.Infrastructure;
using System.Text;

namespace A2v10.ViewEngine.Xaml;

public class XamlPartProvider : IXamlPartProvider
{

	private readonly XamlReaderService _readerService;
	private readonly IAppCodeProvider _codeProvider;

	private readonly ConcurrentDictionary<String, Object?> _cache = new(StringComparer.InvariantCultureIgnoreCase);

	private readonly List<FileSystemWatcher>? _watchers;

	public XamlPartProvider(IAppCodeProvider codeProvider, IOptions<AppOptions> appOptions)
	{
		_readerService = new AppXamlReaderService(this, codeProvider);
		_codeProvider = codeProvider;
		if (appOptions.Value.Environment.Watch)
		{
			// Create watcher for xaml
			var dirs = _codeProvider.EnumerateWatchedDirs(String.Empty, "*.xaml");
			foreach (var dir in dirs) 
			{
				var watcher = new FileSystemWatcher(dir, "*.*")
				{ /* 8.3 file name !!!*/
					NotifyFilter = NotifyFilters.LastWrite //| NotifyFilters.Size | NotifyFilters.Attributes
				};
                watcher.Changed += Watcher_Changed;
                watcher.EnableRaisingEvents = true;
				_watchers ??= [];
                _watchers.Add(watcher);

            }
        }
	}

	private void Watcher_Changed(object sender, FileSystemEventArgs e)
	{
		_cache.Clear();
	}

	public Object? GetCachedXamlPart(String path)
	{
		return _cache.GetOrAdd(path, path => GetXamlPart(path));
	}

	public Object? GetCachedXamlPartOrNull(String path)
	{
		return _cache.GetOrAdd(path, path => GetXamlPartOrNull(path));
	}

	public Object? GetXamlPart(String path)
    {
		path = path.Replace('\\', '/'); // (!)
        using var stream = _codeProvider.FileStreamRO(path)
			?? throw new XamlException($"File not found '{path}'");
		return _readerService.Load(stream, new Uri(path, UriKind.Relative));
	}

	public Object? GetXamlPartText(String text, String path)
	{
		using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
		return _readerService.Load(ms, new Uri(path, UriKind.Relative));
	}

	public Object? GetXamlPartOrNull(String path)
	{
        path = path.Replace('\\', '/'); // (!)
        using var stream = _codeProvider.FileStreamRO(path);
		if (stream == null)
			return null;
		return _readerService.Load(stream, new Uri(path, UriKind.Relative));
	}

	public Task<Object?> GetXamlPartAsync(String path)
	{
		throw new NotImplementedException();
	}
}

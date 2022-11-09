// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace A2v10.ViewEngine.Xaml;

public class XamlPartProviderFile : IXamlPartProvider
{

	private readonly XamlReaderService _readerService;
	private readonly IAppCodeProvider _codeProvider;

	private readonly ConcurrentDictionary<String, Object?> _cache = new(StringComparer.InvariantCultureIgnoreCase);

	private readonly FileSystemWatcher? _watcher;

	public XamlPartProviderFile(IAppCodeProvider codeProvider, IOptions<AppOptions> appOptions)
	{
		_readerService = new AppXamlReaderService(this, codeProvider);
		_codeProvider = codeProvider;
		if (codeProvider.IsFileSystem && appOptions.Value.Environment.Watch)
		{
			var path = _codeProvider.MakeFullPath(String.Empty, String.Empty, false);
			_watcher = new FileSystemWatcher(path, "*.*") /*8.3 file name !!!*/
			{
				NotifyFilter = NotifyFilters.LastWrite //| NotifyFilters.Size | NotifyFilters.Attributes
			};
			_watcher.Changed += Watcher_Changed;
			_watcher.EnableRaisingEvents = true;
		}
	}

	private void Watcher_Changed(object sender, FileSystemEventArgs e)
	{
		_cache.Clear();
	}

	public Object? GetCachedXamlPart(String path)
	{
		if (_cache.TryGetValue(path, out var obj))
			return obj;
		var res = GetXamlPart(path);
		_cache.TryAdd(path, res);
		return res;
	}


	public Object? GetXamlPart(String path)
	{
		var fullPath = _codeProvider.MakeFullPath(String.Empty, path, false);
		using var stream = _codeProvider.FileStreamFullPathRO(fullPath);
		return _readerService.Load(stream, new Uri(fullPath));
	}

	public Task<Object?> GetXamlPartAsync(String path)
	{
		throw new NotImplementedException();
	}
}

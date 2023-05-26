// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;

using Microsoft.Extensions.Options;

using A2v10.Module.Infrastructure;

namespace A2v10.Services;

public class AppCodeProvider : IAppCodeProvider
{
	private readonly Dictionary<String, IAppCodeProviderImpl> _providers = new(StringComparer.InvariantCultureIgnoreCase);
	private readonly String _appId;
	public AppCodeProvider(IOptions<AppOptions> appOptions)
	{
		var opts = appOptions.Value ?? throw new ArgumentNullException(nameof(appOptions));
		if (opts.Modules == null || !opts.Modules.Any())
			_providers.Add("_", CreateProvider(opts.Path));
		else
		{
			foreach (var (k, v) in opts.Modules)
			{
				var key = k.ToLowerInvariant();
				if (v.Default)
					key = "_";
				_providers.Add(key, CreateProvider(v.Path ?? throw new InvalidOperationException("Path is null")));
			}
		}
		_appId = opts.AppId;
	}

	static IAppCodeProviderImpl CreateProvider(String path)
	{
		if (ClrHelpers.IsClrPath(path))
			return new InternalAppCodeProviderClr(CreateContainer(path));
		else
			return new InternalAppCodeProviderFile(path);
	}

	static IAppContainer CreateContainer(String path)
	{
		var assembly = ClrHelpers.ParseClrType(path);
		var container = Activator.CreateInstance(assembly.assembly, assembly.type)?.Unwrap();
		if (container is IAppContainer appContainer)
			return appContainer;
		else
			throw new ArgumentException("Invalid application container");

	}
	IAppCodeProviderImpl GetProvider(String path)
	{
		if (!path.StartsWith("$"))
			return _providers["_"];
		var fx = path.IndexOf('/');
		var key = path[1..fx];
		if (_providers.TryGetValue(key, out var proivder))
			return proivder;
		throw new InvalidOperationException($"Module '{key}' not found");
	}

	public String MakePath(String path, String fileName)
	{
		var relative = Path.GetRelativePath(".", Path.Combine(path, fileName)).NormalizeSlash();
		if (path.StartsWith("$") && !relative.StartsWith("$"))
		{
			int fpos = relative.IndexOf('/');
			relative = relative[(fpos + 1)..];
		}
		return relative;
	}

	public Boolean IsFileExists(String path)
	{
		return GetProvider(path).IsFileExists(path);
	}

	public Stream? FileStreamRO(String path, Boolean primaryOnly = false)
	{
		if (primaryOnly)
			GetProvider("_").FileStreamRO(path);
		return GetProvider(path).FileStreamRO(path);
	}

	public IEnumerable<String> EnumerateAllFiles(String path, String searchPattern)
	{
		foreach (var (k, v) in _providers)
		{
			foreach (var file in v.EnumerateFiles(path, searchPattern))
			{
				if (k != "_")
					yield return $"${k}/{file}";
				else
					yield return file;
			}
		}
	}
	public IEnumerable<String> EnumerateWatchedDirs(String path, String searchPattern)
	{
		foreach (var (_, v) in _providers)
		{
			if (!v.IsFileSystem)
				continue;
			var files = v.EnumerateFiles(path, searchPattern);
			var en = files.GetEnumerator();
			if (en.MoveNext())
			{
				var f = v.NormalizePath(en.Current);
				yield return Path.GetDirectoryName(f) ?? String.Empty;
			}
		}
	}

	public Boolean HasLicensedModules => _providers.Values.Any(p => p.IsLicensed);

	public IEnumerable<Guid> LicensedModules => _providers.Values.Where(p => p.IsLicensed)
		.Select(p => p.ModuleId ?? throw new InvalidOperationException("ModuleId is null"));

	public String AppId => _appId;
}



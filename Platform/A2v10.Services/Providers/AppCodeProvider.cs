// Copyright © 2015-2023 Alex Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using A2v10.App.Abstractions;

namespace A2v10.Services;

public class AppCodeProvider : IAppCodeProvider
{
	public Boolean IsFileSystem => false;

	private readonly Dictionary<String, IAppCodeProviderImpl> _providers = new(StringComparer.InvariantCultureIgnoreCase);
	public AppCodeProvider(IOptions<AppOptions> appOptions)
	{
		var opts = appOptions.Value ?? throw new ArgumentNullException(nameof(appOptions));
		if (opts.Modules == null)
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
        throw new InvalidOperationException($"Provider for '{key}' not found");
	}

	public String MakeFullPath(String path, String fileName, Boolean admin)
	{
		return GetProvider(path).MakeFullPath(path, fileName, admin);
	}

    public String? MakeFullPathCheck(String path, String fileName)
	{
		return GetProvider(path).MakeFullPathCheck(path, fileName);
    }

    public Task<String?> ReadTextFileAsync(String path, String fileName, Boolean admin)
	{
		return GetProvider(path).ReadTextFileAsync(path, fileName, admin);
	}

	public String? ReadTextFile(String path, String fileName, Boolean admin)
	{
		return GetProvider(path).ReadTextFile(path, fileName, admin);	
	}

    public Boolean IsFileExists(String path, String fileName)
	{
        return GetProvider(path).IsFileExists(path, fileName);
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
    public Stream FileStreamRO(String path)
    {
        return GetProvider(path).FileStreamRO(path);
    }

    public IEnumerable<String> EnumerateAllFiles(String path, String searchPattern)
    {
		foreach (var (k, v) in _providers)
		{
			foreach (var file in v.EnumerateFiles(path, searchPattern))
				yield return file;
		}
    }

	public String ReplaceFileName(String baseFullName, String relativeName)
    {
        String dir = Path.GetDirectoryName(baseFullName) ?? String.Empty;
        return Path.GetFullPath(Path.Combine(dir, relativeName));
	}
}



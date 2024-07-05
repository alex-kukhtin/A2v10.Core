// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Reflection;
using System.Text;

using A2v10.Module.Infrastructure;

namespace A2v10.Services;

public class InternalAppCodeProviderClr(IAppContainer _appContainer) : IAppCodeProviderImpl
{
	private String? _moduleVersion = _appContainer.GetType().Assembly.GetName().Version?.ToString();
	public Boolean IsFileSystem => false;
	public Boolean IsLicensed => _appContainer.IsLicensed;
	public Guid? ModuleId => _appContainer.Id;

	public String? ModuleVersion => _moduleVersion;

	public String NormalizePath(String path)
	{
		if (path.StartsWith('$'))
		{
			int ix = path.IndexOf('/');
			path = path[(ix+1).. ];
		}
		return path.Replace('\\', '/');
	}

    public Boolean IsFileExists(String path)
	{
		var fullPath = NormalizePath(path);	
		return _appContainer.FileExists(fullPath);
	}

    public Stream? FileStreamRO(String path)
	{
		var fullPath = NormalizePath(path);
		if (!_appContainer.FileExists(fullPath))
			return null;
        return new MemoryStream(Encoding.UTF8.GetBytes(_appContainer.GetText(fullPath) ?? String.Empty));
    }

    public Stream? FileStreamResource(String path)
    {
		var mainType = _appContainer.GetType();
		var assembly = Assembly.GetAssembly(mainType)
			?? throw new InvalidOperationException($"Assembly with type '{mainType}' not found");
		var resourceName = $"{mainType.Namespace}.{path.Replace('/', '.')}";
		return assembly.GetManifestResourceStream(resourceName);
    }

    public IEnumerable<String> EnumerateFiles(String? path, String searchPattern)
	{
		if (String.IsNullOrEmpty(path))
			return [];
		if (searchPattern.StartsWith('*'))
			searchPattern = searchPattern[1..];
		return _appContainer.EnumerateFiles(path, searchPattern);
	}
}



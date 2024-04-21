// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using A2v10.Infrastructure;
using System.Linq;

namespace A2v10.AppRuntimeBuilder;

public class RuntimeMetadataProvider(IAppCodeProvider _appCodeProvider, IOptions<AppOptions> _appOptions)
{
	private RuntimeMetadata? _runtimeMetdata = null;
    private readonly Boolean _watch = _appOptions.Value.Environment.Watch;
    private FileSystemWatcher? _fileWatcher = null;

    private static readonly JsonSerializerSettings CamelCaseSerializerSettings =
		new()
		{
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore,
			ContractResolver = new DefaultContractResolver()
			{
				NamingStrategy = new CamelCaseNamingStrategy()
			}
		};

	private void CreateWatcher(String fileName)
	{
		if (!_watch) return;
		var path = _appCodeProvider.EnumerateWatchedDirs(String.Empty, fileName).ToList();
		if (path.Count < 1) return;	
		_fileWatcher = new FileSystemWatcher(path[0], fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
        };
        _fileWatcher.Changed += Watcher_Changed;
        _fileWatcher.EnableRaisingEvents = true;

    }
    public async Task<RuntimeMetadata> GetMetadata()
	{
		if (_runtimeMetdata != null)
			return _runtimeMetdata;
		const String fileName = "app.metadata";
        CreateWatcher(fileName);
        using var stream = _appCodeProvider.FileStreamRO(fileName, true)
			?? throw new InvalidOperationException($"{fileName} not found");
		using var sr = new StreamReader(stream);

		var text = await sr.ReadToEndAsync()
			?? throw new InvalidOperationException($"{fileName} is empty");
		var newData = _runtimeMetdata = JsonConvert.DeserializeObject<RuntimeMetadata>(text, CamelCaseSerializerSettings)
			?? throw new InvalidOperationException($"Invalid {fileName}");
		newData.OnEndInit();
		_runtimeMetdata = newData;
		return _runtimeMetdata;
	}

    private void Watcher_Changed(Object sender, FileSystemEventArgs e)
    {
        _runtimeMetdata = null;
    }
}

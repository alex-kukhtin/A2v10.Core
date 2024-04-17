// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

public class RuntimeMetadataProvider(IAppCodeProvider _appCodeProvider)
{
	private RuntimeMetadata? _runtimeMetdata = null;

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

	public async Task<RuntimeMetadata> GetMetadata()
	{
		if (_runtimeMetdata != null)
			return _runtimeMetdata;
		const String fileName = "app.metadata";
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
}

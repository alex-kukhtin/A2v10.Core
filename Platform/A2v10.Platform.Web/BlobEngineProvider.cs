// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Collections.Generic;

using A2v10.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace A2v10.Platform.Web;

public record BlobStorageDescriptor(String Name, Type StorageType);

public class BlobStorageFactory
{
	private readonly List<BlobStorageDescriptor> _list = [];

	public IList<BlobStorageDescriptor> Engines => _list;

	public void RegisterStorage<T>(String name)
	{
		_list.Add(new BlobStorageDescriptor(name, typeof(T)));
	}
}

public class WebBlobStorageProvider(IServiceProvider _serviceProvider, IList<BlobStorageDescriptor> _storages) : IBlobStorageProvider
{
	public IBlobStorage FindBlobStorage(String name)
	{
		var storage = _storages.FirstOrDefault(x => x.Name == name) 
			?? throw new InvalidReqestExecption($"Blob storage for '{name}' not found");
            var rs = _serviceProvider.GetRequiredService(storage.StorageType);
		if (rs is IBlobStorage be)
			return be;
		throw new InvalidReqestExecption($"Blob storage '{storage.StorageType}' is not an IBlobStorage");
	}
}

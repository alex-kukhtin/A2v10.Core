// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using A2v10.Infrastructure;
using System.IO;

namespace FileSystemBlobStorage;

public class AzureBlobStorage(IConfiguration _configuration) : IBlobStorage
{
	private String GetRootPath()
	{
		return _configuration.GetValue<String>("BlobStorage:Path")
			?? throw new InvalidOperationException("BlobStorage:Path not found");
	}
	public async Task<ReadOnlyMemory<Byte>> LoadAsync(String? source, String? container, String blobName)
	{
		var path = Path.Combine(GetRootPath(), blobName);
		var bytes = await File.ReadAllBytesAsync(path);
		return bytes.AsMemory();
	}

	public Task SaveAsync(String? source, String? container, IBlobUpdateInfo blobInfo)
	{
		throw new NotImplementedException();
	}
	public Task DeleteAsync(String? source, String? container, String blobName)
	{
		throw new NotImplementedException();
	}
}

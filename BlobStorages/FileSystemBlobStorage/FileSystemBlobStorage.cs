// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.IO;

using Microsoft.Extensions.Configuration;

using A2v10.Infrastructure;

namespace A2v10.BlobStorage.FileSystem;

public class FileSystemBlobStorage(IConfiguration _configuration) : IBlobStorage
{
	private String GetRootPath()
	{
		return _configuration.GetValue<String>("BlobStorage:Path")
			?? throw new InvalidOperationException("BlobStorage:Path not found");
	}
	public async Task<ReadOnlyMemory<Byte>> LoadAsync(String? source, String? container, String blobName)
	{
		var path = Path.Combine(GetRootPath(), container ?? String.Empty, blobName);
		var bytes = await File.ReadAllBytesAsync(path);
		return bytes.AsMemory();
	}

	public Task SaveAsync(String? source, String? container, IBlobUpdateInfo blobInfo)
	{
		var blobName = blobInfo.BlobName
			?? throw new InvalidOperationException("IBlobUpdateInfo.BlobName is null");
		var blobStream = blobInfo.Stream
			?? throw new InvalidOperationException("IBlobUpdateInfo.Stream is null");
		var path = Path.Combine(GetRootPath(), container ?? String.Empty, blobName);
		EnsureDirectory(path);
		using var ms = new MemoryStream();
		blobStream.CopyTo(ms);
		return File.WriteAllBytesAsync(path, ms.ToArray());
	}

	private void EnsureDirectory(String path)
	{
		var dir = Path.GetDirectoryName(path)
			?? throw new InvalidOperationException("Directory is null");
		if (Directory.Exists(dir))
			return;
		Directory.CreateDirectory(dir);
	}
	public Task DeleteAsync(String? source, String? container, String blobName)
	{
		var path = Path.Combine(GetRootPath(), container ?? String.Empty, blobName);
		File.Delete(path);
		return Task.CompletedTask;
	}
}

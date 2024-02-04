// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using A2v10.Infrastructure;
using System.IO;

namespace A2v10.BlobStorage.Azure;

public class AzureBlobStorage(IConfiguration _configuration) : IBlobStorage
{
	private readonly ConcurrentDictionary<String, BlobContainerClient> _containers = new();

	private const String DefaultConnectionString = "AzureStorage";
	private const String DefaultContainerName = "default";	

	private BlobContainerClient GetClient(String? source, String? container)
	{
		if (String.IsNullOrEmpty(source))
			source = DefaultConnectionString;
		if (String.IsNullOrEmpty(container))
			container = DefaultContainerName;
		var key = $"{source}:{container}";
		if (_containers.TryGetValue(key, out BlobContainerClient? client))
			return client;
		var cnnStr = _configuration.GetConnectionString(source)
			?? throw new InvalidOperationException($"Connection String {source} not found");
		var serviceClient = new BlobServiceClient(cnnStr);
		client = serviceClient.GetBlobContainerClient(container);
		_containers.TryAdd(key, client);
		return client;
	}
	public async Task<ReadOnlyMemory<Byte>> LoadAsync(String? source, String? container, String blobName)
	{
		var client = GetClient(source, container);
		var content = await client.GetBlobClient(blobName).DownloadContentAsync();
		return content.Value.Content.ToMemory();
	}

	public async Task SaveAsync(String? source, String? container, IBlobUpdateInfo blobInfo)
	{
		var client = GetClient(source, container);
		await client.UploadBlobAsync(blobInfo.BlobName, blobInfo.Stream);
	}

	public async Task SaveAsync(String? source, String? container, String blobName, Stream stream)
	{
		var client = GetClient(source, container);
		await client.UploadBlobAsync(blobName, stream);
	}

	public Task DeleteAsync(String? source, String? container, String blobName)
	{
		var client = GetClient(source, container);
		return client.GetBlobClient(blobName).DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots);
	}
}

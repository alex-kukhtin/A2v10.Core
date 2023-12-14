

using System;
using System.Data;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using Azure.Storage.Blobs;

namespace A2v10.AzureBlob;

public class AzureBlobStorage : IBlobStorage
{
	public AzureBlobStorage()
	{
		var client = new BlobContainerClient("cnnstr", "container");
		client.Create();
		int z = 5;
	}

	public Task LoadAsync(String blobName)
	{
		throw new NotImplementedException();
	}

	public Task SaveAsync(String source, IBlobUpdateInfo blobInfo)
	{
		throw new NotImplementedException();
	}
}

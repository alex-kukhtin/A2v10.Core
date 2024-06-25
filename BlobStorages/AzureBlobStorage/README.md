# About

A2v10.BlobStorage.Azure is a part of A2v10.Platform.

# How to use

```csharp
services.AddBlobStorages(factory =>
{
	factory.RegisterStorage<AzureBlobStorage>("AzureStorage");
});
```

## Configuration (appsettings.json)
```json
"ConnectionStrings": {
	"AzureStorage": "DefaultEndpointsProtocol=https;AccountName=azure_account_name;AccountKey=azure_account_key;EndpointSuffix=core.windows.net",
}
```

If you use **"blobStorage": "FromConfig"** in model.json, then specify the provider:
```json
"BlobStorage": {
	"Provider": "AzureStorage"
}
```


# Related Packages

* [A2v10.Platform](https://www.nuget.org/packages/A2v10.Platform)
* [A2v10.BlobStorage.FileSystem](https://www.nuget.org/packages/A2v10.BlobStorage.FileSystem)

# Feedback

A2v10.BlobStorage.Azure is released as open source under the MIT license.
Bug reports and contributions are welcome at the GitHub repository.

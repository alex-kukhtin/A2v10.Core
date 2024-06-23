# About

A2v10.BlobStorage.FileSystem is a part of A2v10.Platform.

# How to use

```csharp
services.AddBlobStorages(factory =>
{
	factory.RegisterStorage<FileSystemBlobStorage>("FileSystem");
});

/* or */
services.AddBlobStorages(factory =>
{
	if (configuration.GetValue<String>("BlobStorage:Provider") == "FileSystem")
		factory.RegisterStorage<AzureBlobStorage>("FileSystem");
});
```

## Configuration (appsettings.json)
```json
"BlobStorage": {
	"Provider":"FileSystem",
	"Path": "server_path"
}
```

# Related Packages

* [A2v10.Platform](https://www.nuget.org/packages/A2v10.Platform)
* [A2v10.BlobStorage.Azure](https://www.nuget.org/packages/A2v10.BlobStorage.Azure)

# Feedback

A2v10.BlobStorage.FileSystem is released as open source under the MIT license.
Bug reports and contributions are welcome at the GitHub repository.

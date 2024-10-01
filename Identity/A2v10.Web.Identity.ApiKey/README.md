# About
A2v10.Identity.ApiKey allows the ApiKey authorization 
for the A2v10 platform applications.

# How to use

The ApiKey may be plain text (*KeyType.ApiKey*) or contain encoded user claims (*KeyType.EncodedClaims*).

If you use *KeyType.EncodedClaims*, you must specify the encryption key and vector. 

Use 
```csharp
ApiKeyUserHelper<T>.GenerateApiKey(User, Сonfiguration)
``` 
for generate encoded ApiKey.

```csharp
// T - type of user identifier
.AddApiKeyAuthorization<T>(options => {
    // options is read only
});

// configure
services.Configure<ApiKeyConfigurationOptions>(options =>
{
	options.KeyType = KeyType.EncodedClaims; /* or KeyType.ApiKey */
	/* for KeyType.EncodedClaims */
	options.AesEncryptKey = "Aes_Encrypt_Key";
	options.AesEncryptVector = "Aes_Encrypt_Vector";
	options.SkipCheckUser = true;
	/* or */
	options.Configure<Int64>(KeyType.EncodedClaims, Configuration);
});
```

## appsettings.json (for KeyType.EncodedClaims)
```json
"AesEncrypt": {
	"Key": "my_encrypt_key_1", /* 16 chars min */
	"Vector": "my_encrypt_vector_1" /* 16 chars min */
}
```

# Related Packages

* [A2v10.Identity.Core](https://www.nuget.org/packages/A2v10.Identity.Core)


# Feedback

A2v10.Identity.ApiKey is released as open source under the MIT license. 
Bug reports and contributions are welcome at the GitHub repository.
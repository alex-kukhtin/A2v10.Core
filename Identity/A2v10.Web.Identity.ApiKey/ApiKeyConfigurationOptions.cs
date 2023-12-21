// Copyright © 2020-2023 Alex Kukhtin. All rights reserved.

using System;

using Microsoft.Extensions.Configuration;

using A2v10.Identity.Core;

namespace A2v10.Web.Identity.ApiKey;

public enum KeyType
{
	ApiKey,
	EncodedClaims
}

public record ApiKeyConfigurationOptions
{
	public KeyType KeyType { get; set; }
	public String AesEncryptKey { get; set; } = String.Empty;
	public String AesEncryptVector { get; set; } = String.Empty;
	public Boolean SkipCheckUser { get; set; }	
	public void Configure<T>(KeyType keyType, IConfiguration configuration) where T : struct
	{
		KeyType = keyType;

		AesEncryptKey = configuration.GetValue<String>(ApiKeyUserHelper<T>.ConfigKey)
			?? throw new InvalidOperationException($"{ApiKeyUserHelper<T>.ConfigKey} is null");
		AesEncryptVector = configuration.GetValue<String>(ApiKeyUserHelper<T>.ConfigVector)
			?? throw new InvalidOperationException($"{ApiKeyUserHelper<T>.ConfigVector} is null");
	}
}

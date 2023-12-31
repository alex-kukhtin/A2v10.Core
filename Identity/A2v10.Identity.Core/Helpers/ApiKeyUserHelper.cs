// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using A2v10.Web.Identity;
using Microsoft.Extensions.Configuration;

namespace A2v10.Identity.Core;

public static class ApiKeyUserHelper<T> where T : struct
{
	public const String ConfigKey = "AesEncrypt:Key";
	public const String ConfigVector = "AesEncrypt:Vector";

	private static JsonSerializerOptions DefaultSerializerOptions => new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault};
	public static String GenerateApiKey(AppUser<T> appUser, String key, String vector)
	{
		var user = new AppUser<T> { 
			Id = appUser.Id,
			Tenant = appUser.Tenant,
			Segment = appUser.Segment,
			Locale = appUser.Locale,
			UserName = Guid.NewGuid().ToString()	
		};
		var jsonUser = JsonSerializer.Serialize(user, DefaultSerializerOptions);
		return AesEncrypt.EncryptString(jsonUser, key, vector);
	}


	private static (String Key, String Vector) GetKeyVector(IConfiguration configuration)
	{
		var key = configuration.GetValue<String>(ConfigKey)
			?? throw new InvalidOperationException($"{ConfigKey} is null");
		var vector = configuration.GetValue<String>(ConfigVector)
			?? throw new InvalidOperationException($"{ConfigVector} is null");
		return (Key: key, Vector: vector);
	}
	public static String GenerateApiKey(AppUser<T> appUser, IConfiguration configuration)
	{
		var (Key, Vector) = GetKeyVector(configuration);
		return GenerateApiKey(appUser, Key, Vector);	
	}

	public static AppUser<T>? GetUserFromApiKey(String apiKey, String key, String vector)
	{
		var userAsString = AesEncrypt.DecryptString(apiKey, key, vector);
		return JsonSerializer.Deserialize<AppUser<T>>(userAsString);
	}

	public static AppUser<T>? GetUserFromApiKey(String apiKey, IConfiguration configuration)
	{
		var (Key, Vector) = GetKeyVector(configuration);
		return GetUserFromApiKey(apiKey, Key, Vector);
	}
}

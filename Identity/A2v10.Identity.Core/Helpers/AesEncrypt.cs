// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Security.Cryptography;
using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace A2v10.Web.Identity;

public static class AesEncrypt
{
	public static String EncryptString(String text, String key, String iv)
	{
		using var aes = Aes.Create();

		aes.Key = Encoding.UTF8.GetBytes(key)[0..16];
		aes.IV = Encoding.UTF8.GetBytes(iv)[0..16];

		var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

		var ms = new MemoryStream();
		using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
		using (var sw = new StreamWriter(cs))
		{
			sw.Write(text);
		}
		return Base64UrlEncoder.Encode(ms.ToArray());
	}

	public static String DecryptString(String text, String key, String iv)
	{
		using var aes = Aes.Create();

		aes.Key = Encoding.UTF8.GetBytes(key)[0..16];
		aes.IV = Encoding.UTF8.GetBytes(iv)[0..16];

		var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

		Byte[] decoded = Base64UrlEncoder.DecodeBytes(text);

		using var ms = new MemoryStream(decoded);
		using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
		using var sr = new StreamReader(cs);
		return sr.ReadToEnd();
	}
}

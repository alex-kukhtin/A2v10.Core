// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;
using System.Runtime;
using System.Security.Cryptography;
using System.Text;

namespace A2v10.Services;

public static class SignatureProvider
{
	public class Keys
	{
		public String Private = String.Empty;
		public String Public = String.Empty;
	}

	public static Keys CreateKeys()
	{
		using SHA256 alg = SHA256.Create();

		// Generate signature
		using RSA rsa = RSA.Create();
		var keys = new Keys()
		{
			Private = Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
			Public = Convert.ToBase64String(rsa.ExportRSAPublicKey())
		};
		return keys;
	}

	public static String Sign(String source, String privateKey)
	{
		using SHA256 alg = SHA256.Create();
		using RSA rsa = RSA.Create();
		rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out int bytesRead);
		RSAPKCS1SignatureFormatter rsaFormatter = new(rsa);
		rsaFormatter.SetHashAlgorithm(nameof(SHA256));

		byte[] data = Encoding.ASCII.GetBytes(source);
		byte[] hash = alg.ComputeHash(data);

		var signedHash = rsaFormatter.CreateSignature(hash);
		return Convert.ToBase64String(signedHash);
	}

	public static Boolean VerifySign(String text, String signature, String publicKey)
	{
		using SHA256 alg = SHA256.Create();
		using RSA rsa = RSA.Create();
		rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out int _);

		RSAPKCS1SignatureDeformatter rsaDeformatter = new(rsa);
		rsaDeformatter.SetHashAlgorithm(nameof(SHA256));

		byte[] data = Encoding.ASCII.GetBytes(text);
		byte[] hash = alg.ComputeHash(data);

		return rsaDeformatter.VerifySignature(hash, Convert.FromBase64String(signature));
	}

    private static JsonSerializerSettings NoFormatSettings = new JsonSerializerSettings()
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None
    };

    public static String SignData(String data, String privateKey)
    {
        var licInfo = JsonConvert.DeserializeObject<ExpandoObject>(data)
            ?? throw new InvalidOperationException("Deserialize failed");

        licInfo.Set("Signature", null);
        var source = JsonConvert.SerializeObject(licInfo, NoFormatSettings);

        var signature = Sign(source, privateKey);
        licInfo.Set("Signature", signature);
        var target = JsonConvert.SerializeObject(licInfo)
            ?? throw new InvalidOperationException("Serialize failed");

		return target;
    }
    public static String SignData(ExpandoObject data, String privateKey)
	{
        data.Set("Signature", null);
        var source = JsonConvert.SerializeObject(data, NoFormatSettings);
        var signature = Sign(source, privateKey);
        data.Set("Signature", signature);
        var target = JsonConvert.SerializeObject(data)
            ?? throw new InvalidOperationException("Serialize failed");
        return target;
    }

    public static Boolean VerifyData(String data, String publicKey)
    {
        var licInfo = JsonConvert.DeserializeObject<ExpandoObject>(data)
            ?? throw new InvalidOperationException("Ivalid license file");
        var signature = licInfo.Get<String>("Signature")
            ?? throw new InvalidOperationException("Signature not found");
        licInfo.Set("Signature", null);
        var str = JsonConvert.SerializeObject(licInfo, NoFormatSettings);
        return VerifySign(str, signature, publicKey);
    }

    public static Boolean VerifyData(ExpandoObject data, String publicKey)
    {
        var signature = data.Get<String>("Signature")
            ?? throw new InvalidOperationException("Signature not found");
        data.Set("Signature", null);
        var str = JsonConvert.SerializeObject(data, NoFormatSettings);
        return VerifySign(str, signature, publicKey);
    }
}

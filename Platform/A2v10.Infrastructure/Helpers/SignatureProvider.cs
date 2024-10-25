// Copyright © 2023-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Security.Cryptography;
using System.Text;

namespace A2v10.Infrastructure;

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
			Private = Convert.ToHexString(rsa.ExportRSAPrivateKey()),
			Public = Convert.ToHexString(rsa.ExportRSAPublicKey())
		};
		return keys;
	}

	public static String Sign(String source, String privateKey)
	{
		using SHA256 alg = SHA256.Create();
		using RSA rsa = RSA.Create();
		rsa.ImportRSAPrivateKey(Convert.FromHexString(privateKey), out int bytesRead);
		RSAPKCS1SignatureFormatter rsaFormatter = new(rsa);
		rsaFormatter.SetHashAlgorithm(nameof(SHA256));

		byte[] data = Encoding.ASCII.GetBytes(source);
		byte[] hash = alg.ComputeHash(data);

		var signedHash = rsaFormatter.CreateSignature(hash);
		return Convert.ToHexString(signedHash);
	}

	public static Boolean VerifySign(String text, String signature, String publicKey)
	{
		using SHA256 alg = SHA256.Create();
		using RSA rsa = RSA.Create();
		rsa.ImportRSAPublicKey(Convert.FromHexString(publicKey), out int _);

		RSAPKCS1SignatureDeformatter rsaDeformatter = new(rsa);
		rsaDeformatter.SetHashAlgorithm(nameof(SHA256));

		byte[] data = Encoding.ASCII.GetBytes(text);
		byte[] hash = alg.ComputeHash(data);

		return rsaDeformatter.VerifySignature(hash, Convert.FromHexString(signature));
	}
}

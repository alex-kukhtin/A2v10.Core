// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System.Text;
using System.Security.Cryptography;
using System.Collections.Immutable;

using Newtonsoft.Json;

namespace A2v10.Services;

internal class ModuleInfo
{
	public Guid Id { get; set; }
}
internal class LicenseInfo
{
	public Guid Id { get; set; }
	public String? Email { get; set; }
	public String? Name { get; set; }
	public Int32 KeyVersion { get; set; }
	public Guid IssuerId { get; set; }
	public String? IssuedBy { get; set; }
	public DateTime IssuedOn { get; set; }
	public DateTime ExpiresOn { get; set; }
	public List<ModuleInfo> Modules { get; set; } = new();
	public String? Signature { get; set; }
}

internal partial class LicenseManager
{
	private String FindKey(Guid IssuerId, Int32 keyVersion)
	{
		var key = $"{IssuerId.ToString().ToUpperInvariant()}:{keyVersion}";
		if (_publicKeys.TryGetValue(key, out var result))
			return result;
		throw new InvalidOperationException("License public key not found");
	}
	public Boolean VerifyLicense(String license)
	{
		var licInfo = JsonConvert.DeserializeObject<LicenseInfo>(license)
			?? throw new InvalidOperationException("Ivalid license");
		var signature = licInfo.Signature
			?? throw new InvalidOperationException("Ivalid license");
		licInfo.Signature = null;
		var str = JsonConvert.SerializeObject(licInfo, new JsonSerializerSettings()
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.None
		});
		var publicKey = FindKey(licInfo.IssuerId, licInfo.KeyVersion);
		return VerifySignature(str, signature, publicKey);
	}

	private Boolean VerifySignature(String text, String signature, String publicKey)
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

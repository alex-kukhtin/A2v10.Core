// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Web;

using Microsoft.Extensions.Configuration;

using A2v10.BlobStorage.Azure;

namespace TestServices;

/* GetReadUrl: read only, the default source and container, expiry = now + lifetime.
 * Offline: the SAS is signed by the well-known Azurite key, no emulator and no network.
 */
[TestClass]
[TestCategory("Blobs")]
public class AzureBlobStorageTests
{
	[TestMethod]
	public void ReadUrl()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<String, String?>() { ["ConnectionStrings:AzureStorage"] = "UseDevelopmentStorage=true" })
			.Build();
		var lifetime = TimeSpan.FromMinutes(10);
		var before = DateTimeOffset.UtcNow;
		var url = new AzureBlobStorage(config).GetReadUrl(null, null, "a/b.pdf", lifetime);
		var after = DateTimeOffset.UtcNow;

		Assert.AreEqual("/devstoreaccount1/default/a/b.pdf", url.AbsolutePath);
		var query = HttpUtility.ParseQueryString(url.Query);
		Assert.AreEqual("r", query["sp"]);
		// the SAS keeps whole seconds
		var expires = DateTimeOffset.Parse(query["se"]!);
		Assert.IsTrue(expires >= before.Add(lifetime).AddSeconds(-1) && expires <= after.Add(lifetime), $"se={expires:O}");
	}
}

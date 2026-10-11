// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.IO;

using Microsoft.Extensions.Configuration;

using A2v10.BlobStorage.FileSystem;
using A2v10.Services;

namespace TestServices;

/* A path from the url or an uploaded file's name never leaves the root it is resolved against.
 * The bound stands once, at the bottom, in whatever opens the file - not in the controllers.
 */
[TestClass]
[TestCategory("PathBound")]
public class PathBoundTests
{
	private static String NewSandbox()
	{
		// sandbox/secret.png lies beside the root, sandbox/root/images/a.png inside it
		var sandbox = Path.Combine(Path.GetTempPath(), "a2v10_pathbound", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(sandbox, "root", "images"));
		File.WriteAllText(Path.Combine(sandbox, "root", "images", "a.png"), "a");
		File.WriteAllText(Path.Combine(sandbox, "secret.png"), "secret");
		return sandbox;
	}

	[TestMethod]
	public void Provider_serves_paths_inside_the_root()
	{
		var sandbox = NewSandbox();
		var provider = new InternalAppCodeProviderFile(Path.Combine(sandbox, "root"));

		Assert.IsTrue(provider.IsFileExists("images/a.png"));
		Assert.IsTrue(provider.IsFileExists("$module/images/a.png"));
		Assert.IsTrue(provider.IsFileExists("images/../images/a.png"));
		using var stream = provider.FileStreamRO("images/a.png");
		Assert.IsNotNull(stream);
	}

	[TestMethod]
	public void Provider_refuses_paths_outside_the_root()
	{
		var sandbox = NewSandbox();
		var provider = new InternalAppCodeProviderFile(Path.Combine(sandbox, "root"));

		Assert.ThrowsExactly<UnauthorizedAccessException>(() => provider.FileStreamRO("../secret.png"));
		Assert.ThrowsExactly<UnauthorizedAccessException>(() => provider.FileStreamRO(Path.Combine(sandbox, "secret.png")));
		Assert.ThrowsExactly<UnauthorizedAccessException>(() => provider.FileStreamRO("$module/../secret.png"));
		// /_static_image turns '-' into '.' after the route is parsed
		Assert.ThrowsExactly<UnauthorizedAccessException>(() => provider.FileStreamRO("--/secret-png".Replace('-', '.')));
		// a sibling whose name starts with the root's is outside too
		Directory.CreateDirectory(Path.Combine(sandbox, "rootx"));
		File.WriteAllText(Path.Combine(sandbox, "rootx", "b.png"), "b");
		Assert.ThrowsExactly<UnauthorizedAccessException>(() => provider.FileStreamRO("../rootx/b.png"));
	}

	private static FileSystemBlobStorage Storage(String sandbox)
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<String, String?>() { ["BlobStorage:Path"] = Path.Combine(sandbox, "root") })
			.Build();
		return new FileSystemBlobStorage(config);
	}

	private static BlobUpdateInfo Blob(String name) => new()
	{
		BlobName = name,
		Stream = new MemoryStream([1, 2, 3])
	};

	[TestMethod]
	public async Task Storage_keeps_blobs_inside_the_root()
	{
		var sandbox = NewSandbox();
		var storage = Storage(sandbox);

		await storage.SaveAsync(null, "container", Blob("1/folder/x.bin"));
		var bytes = await storage.LoadAsync(null, "container", "1/folder/x.bin");
		CollectionAssert.AreEqual(new Byte[] { 1, 2, 3 }, bytes.ToArray());
		await storage.DeleteAsync(null, "container", "1/folder/x.bin");
	}

	[TestMethod]
	public async Task Storage_refuses_blobs_outside_the_root()
	{
		var sandbox = NewSandbox();
		var storage = Storage(sandbox);

		// the name of an upload rides the blob name: {tenant}{folder}/{guid}_{Name}
		await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => storage.SaveAsync(null, null, Blob("1/f/guid_/../../../../evil.bin")));
		Assert.IsFalse(File.Exists(Path.Combine(sandbox, "evil.bin")));
		await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => storage.LoadAsync(null, null, "../secret.png"));
		await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => storage.LoadAsync(null, null, Path.Combine(sandbox, "secret.png")));
		await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => storage.LoadAsync(null, "..", "secret.png"));
		await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => storage.DeleteAsync(null, null, "../secret.png"));
		Assert.IsTrue(File.Exists(Path.Combine(sandbox, "secret.png")));
	}
}

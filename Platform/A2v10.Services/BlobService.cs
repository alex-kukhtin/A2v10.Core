// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;

namespace A2v10.Services;

internal record BlobInsertInfo
{
	public Int64 UserId { get; init; }
	public String Name { get; init; } = String.Empty;
	public String Mime { get; init; } = String.Empty;
	public String BlobName { get; init; } = String.Empty;
	public Byte[]? Hash { get; init; }
	// an attachment; the owner's key as text - platformid may be bigint or uniqueidentifier, the parameter converts
	public String? Base { get; init; }
	public String? LinkId { get; init; }
}

internal record BlobInsertOutput
{
	public Int64 Id { get; set; }
	public Guid AccessToken { get; set; }
	public Int64? Link { get; set; }
}

internal record BlobLoadOutput
{
	public String Name { get; set; } = String.Empty;
	public String Mime { get; set; } = String.Empty;
	public String BlobName { get; set; } = String.Empty;
	public String? ThumbName { get; set; }
	public Byte[]? Hash { get; set; }
	public Guid AccessToken { get; set; }
}

// IBlobStorageProvider is resolved on use: it is registered only by an application that calls AddBlobStorages
public class BlobService(IServiceProvider _serviceProvider, IModelJsonReader _modelReader, IDbContext _dbContext, ICurrentUser _currentUser, IConfiguration _configuration, ITokenProvider _tokenProvider) : IBlobService
{
	public async Task<BlobUploadResult> UploadAsync(String path, String name, String mime, Stream stream)
	{
		var (url, blob) = await DoorAsync(path);
		if (blob.RequireBase)
			throw new DataServiceException($"'{url.LocalPath}' requires base: an upload there links the file to an owner");
		var (output, realMime, hash) = await StoreAsync(blob, name, mime, stream, null, null);
		return Uploaded(output, name, realMime, hash);
	}

	public async Task<BlobLinkUploadResult> UploadLinkAsync(String path, String @base, String owner, String name, String mime, Stream stream)
	{
		var (url, blob) = await DoorAsync(path);
		if (!blob.RequireBase)
			throw new DataServiceException($"'{url.LocalPath}' has no requireBase: nothing to link a file to");
		if (String.IsNullOrEmpty(owner))
			throw new DataServiceException("An upload into an attachment requires owner");
		await BaseAccess.CheckAsync(_modelReader, _currentUser, @base);
		var (output, realMime, hash) = await StoreAsync(blob, name, mime, stream, @base, owner);
		var link = output.Link ?? throw new InvalidOperationException("Blob.Insert returned no link");
		return new BlobLinkUploadResult(link, Uploaded(output, name, realMime, hash));
	}

	// "<endpoint>/<Key>": the trailing slash leaves the url without an id - Action = Key, LocalPath = endpoint
	async Task<(PlatformUrl Url, IModelBlobDoor Blob)> DoorAsync(String path)
	{
		var url = new PlatformUrl(UrlKind.Image, path + '/');
		var blob = await _modelReader.GetBlobDoorAsync(url)
			?? throw new DataServiceException($"Blob '{url.Action}' not found in '{url.LocalPath}/model.json'");
		DataService.CheckAccess(_currentUser, blob, write: true);
		return (url, blob);
	}

	// every check before Storage and the database; the blob and its link are one insert
	async Task<(BlobInsertOutput Output, String Mime, Byte[]? Hash)> StoreAsync(IModelBlobDoor blob, String name, String mime, Stream stream,
		String? @base, String? owner)
	{
		mime = await RealMimeAsync(stream, mime);
		if (!IsAccepted(blob.Accept, mime))
			throw new DataServiceException($"UI:Invalid file type '{mime}'");
		if (blob.MaxSize != null && stream.Length > blob.MaxSize * 1024L)
			throw new DataServiceException($"UI:File is too large (max {blob.MaxSize} KB)");

		var blobName = $"{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid()}_{name}";
		var info = new BlobUpdateInfo()
		{
			Name = name,
			Mime = mime,
			BlobName = blobName,
			Stream = stream
		};
		await info.ComputeHashAsync();
		await Storage().SaveAsync(null, null, info);

		var output = await _dbContext.ExecuteAndLoadAsync<BlobInsertInfo, BlobInsertOutput>(blob.DataSource, "a2blob.[Blob.Insert]", new BlobInsertInfo()
		{
			UserId = _currentUser.Identity.Id ?? throw new InvalidOperationException("UserId is null"),
			Name = name,
			Mime = mime,
			BlobName = blobName,
			Hash = info.Hash,
			Base = @base,
			LinkId = owner
		}) ?? throw new InvalidOperationException("Blob.Insert returned nothing");
		return (output, mime, info.Hash);
	}

	BlobUploadResult Uploaded(BlobInsertOutput output, String name, String mime, Byte[]? hash) =>
		new(output.Id, _tokenProvider.GenerateToken(output.AccessToken), name, mime, hash);

	public async Task<BlobContent> LoadAsync(Int64 id, String token, Boolean thumb)
	{
		// before the storage: a wrong token reads nothing
		var blob = await LoadCheckedAsync(id, token);
		var isThumb = thumb && blob.ThumbName != null;
		var data = await Storage().LoadAsync(null, null, isThumb ? blob.ThumbName! : blob.BlobName);
		// the hash is the original's: a thumbnail travels without one
		return new BlobContent(blob.Name, blob.Mime, isThumb ? null : blob.Hash, data);
	}

	/* Save: a non-empty reference <model>.<Key> must carry the token that Load or the upload issued,
	 * or a guessed Id makes the next Load issue a token for any blob of the database. Empty is an
	 * object with Id 0 (or null) and is always allowed. A bare scalar is a reference without a token.
	 * The token field is named Token, as in the upload response. The model is the door's: the endpoint's
	 * own, or the one a synthesized door names - the model of a metadata view is '$meta', which routes
	 */
	public async Task CheckReferencesAsync(IModelView view, ExpandoObject data)
	{
		foreach (var (key, door) in view.BlobDoors)
		{
			if (FindIgnoreCase(data, door.CurrentModel) is not ExpandoObject root)
				continue;
			var value = FindIgnoreCase(root, key);
			var reference = value as ExpandoObject;
			var id = Convert.ToInt64(reference != null ? FindIgnoreCase(reference, "Id") ?? 0 : value ?? 0);
			if (id == 0)
				continue;
			var token = reference != null ? FindIgnoreCase(reference, "Token")?.ToString() : null;
			await LoadCheckedAsync(id, token);
		}
	}

	async Task<BlobLoadOutput> LoadCheckedAsync(Int64 id, String? token)
	{
		// an endpoint with blobs has no own source: a2blob lives in the default database
		var prms = new ExpandoObject() { { "Id", id } };
		var blob = await _dbContext.LoadAsync<BlobLoadOutput>(null, "a2blob.[Blob.Load]", prms)
			?? throw new DataServiceException($"Blob not found ({id})");
		if (token == null || _tokenProvider.GenerateToken(blob.AccessToken) != token)
			throw new DataServiceException($"Invalid blob token ({id})");
		return blob;
	}

	static Object? FindIgnoreCase(ExpandoObject eo, String? name)
	{
		foreach (var (k, v) in eo)
			if (k.Equals(name, StringComparison.OrdinalIgnoreCase))
				return v;
		return null;
	}

	IBlobStorage Storage()
	{
		var provider = _configuration.GetValue<String>("BlobStorage:Provider")
			?? throw new InvalidOperationException("BlobStorage:Provider not found");
		if (provider == "SqlServer")
			throw new NotSupportedException("The SqlServer blob provider is not implemented yet");
		return _serviceProvider.GetRequiredService<IBlobStorageProvider>().FindBlobStorage(provider);
	}

	// The bytes win for the inline types; otherwise the type by the name stays.
	// The stream is seekable: IFormFile's always is, and Length/Position fail loudly on any other
	static async Task<String> RealMimeAsync(Stream stream, String mimeByName)
	{
		var header = new Byte[BlobSignature.HeaderLength];
		var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
		stream.Position = 0;
		var detected = BlobSignature.Detect(header.AsSpan(0, read));
		if (detected != null)
			return detected;
		if (BlobSignature.IsKnown(mimeByName))
			throw new DataServiceException("UI:The file content does not match its type");
		return mimeByName;
	}

	// "image/png,image/jpeg" or "image/*"; no accept - anything
	static Boolean IsAccepted(String? accept, String mime)
	{
		if (String.IsNullOrEmpty(accept))
			return true;
		foreach (var a in accept.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
		{
			if (a.Equals(mime, StringComparison.OrdinalIgnoreCase))
				return true;
			if (a.EndsWith("/*") && mime.StartsWith(a[..^1], StringComparison.OrdinalIgnoreCase))
				return true;
		}
		return false;
	}
}

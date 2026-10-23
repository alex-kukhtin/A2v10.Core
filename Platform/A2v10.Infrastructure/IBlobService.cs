// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public record BlobUploadResult(Int64 Id, String Token, String Name, String Mime, Byte[]? Hash);
// an attachment: the link row (Id) and its blob - the shape of a row of a2blob.BlobLinks with its Blob
public record BlobLinkUploadResult(Int64 Id, BlobUploadResult Blob);
public record BlobContent(String Name, String Mime, Byte[]? Hash, ReadOnlyMemory<Byte> Data);

// The token (AccessToken + user) is issued and checked here, in one place
public interface IBlobService
{
	// path = "<endpoint>/<Key>", e.g. "catalog/agentblob/Logo"; mime = the type by the file name,
	// never the client's Content-Type; the inline types are taken from the bytes
	Task<BlobUploadResult> UploadAsync(String path, String name, String mime, Stream stream);
	// into an endpoint with requireBase: the blob and its link to owner under base, one transaction
	Task<BlobLinkUploadResult> UploadLinkAsync(String path, String @base, String owner, String name, String mime, Stream stream);
	// thumb without a thumbnail returns the original
	Task<BlobContent> LoadAsync(Int64 id, String token, Boolean thumb);
	// before Save: every non-empty reference <model>.<Key> carries its token
	Task CheckReferencesAsync(IModelView view, ExpandoObject data);
}

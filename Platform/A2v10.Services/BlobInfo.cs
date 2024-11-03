// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;
using System.Text;

namespace A2v10.Services;
public class BlobInfo : IBlobInfo
{
	public String? Mime { get; init; }
	public String? Name { get; init; }
	public Guid Token { get; init; }
	public Byte[]? Stream { get; set; }
	public String? BlobName { get; init; }
    public Boolean SkipToken { get; init; }
    public Boolean CheckToken => !SkipToken;
}

public class BlobTextInfo : IBlobInfo
{
    public String? Mime { get; init; }
    public String? Name { get; init; }
    public Guid Token { get; init; }
    public String? Data { get; init; }   
    public Byte[]? Stream => Encoding.UTF8.GetBytes(Data ?? String.Empty);
    public String? BlobName { get; init; }
    public Boolean SkipToken { get; init; }
    public Boolean CheckToken => !SkipToken;
}

public record BlobUpdateInfo : IBlobUpdateInfo
{
    public Int32? TenantId { get; set; }
    public Int64? CompanyId { get; set; }
    public Int64 UserId { get; set; }
    public String? Mime { get; set; }
    public String? Name { get; set; }
    public Stream? Stream { get; set; }
    public String? BlobName { get; set; }
    public String? Key { get; set; }
    public Object? Id { get; set; }
}

public record BlobUpdateString
{
	public Int32? TenantId { get; set; }
	public Int64 UserId { get; set; }
	public String? Mime { get; set; }
	public String? Name { get; set; }
	public String? Stream { get; set; }
	public String? Key { get; set; }
	public Object? Id { get; set; }
    public Stream? RawData { get; set; }
}

public record BlobUpdateOutput : IBlobUpdateOutput
{
    public Object Id { get; set; } = new();
    public String? Name { get; set; }
    public Guid? Token { get; set; }
}

// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System.IO;

namespace A2v10.Services;
public class BlobInfo : IBlobInfo
{
	public String? Mime { get; set; }
	public String? Name { get; set; }
	public Guid Token { get; set; }
	public Byte[]? Stream { get; set; }
	public String? BlobName { get; set; }
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
public record BlobUpdateOutput : IBlobUpdateOutput
{
    public Object? Id { get; set; }
    public Guid? Token { get; set; }
}

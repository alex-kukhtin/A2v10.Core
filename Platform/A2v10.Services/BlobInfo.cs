// Copyright © 2021 Alex Kukhtin. All rights reserved.

namespace A2v10.Services;
public class BlobInfo : IBlobInfo
{
	public String? Mime { get; set; }
	public String? Name { get; set; }
	public Guid Token { get; set; }
	public Byte[]? Stream { get; set; }
	public String? BlobName { get; set; }
}


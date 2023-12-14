// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.


using System;
using System.Dynamic;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

public record InvokeBlobResult
{
	public String? Name { get; init; }
    public String? Mime { get; init; }
    public Byte[]? Stream { get; init; }

	public String? BlobName { get; init; }
}

public interface IClrInvokeBlob
{
	Task<InvokeBlobResult> InvokeAsync(ExpandoObject args);
}

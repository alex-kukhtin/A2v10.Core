// Copyright © 2022 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.ReportEngine.Pdf;

public record PdfInvokeResult : IInvokeResult
{
	public PdfInvokeResult(Byte[] body, String? fileName = null)
	{
		Body = body;
		FileName = fileName;
	}

	public Byte[] Body { get; init; }
	public String ContentType => MimeTypes.Application.Pdf;
	public String? FileName{ get; init; }

	public ISignalResult? Signal => null; 
}

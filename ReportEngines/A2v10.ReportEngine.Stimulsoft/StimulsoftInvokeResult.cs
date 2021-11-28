// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.ReportEngine.Stimulsoft
{
	public record StimulsoftInvokeResult(Byte[] Body, String ContentType, String FileName) : IInvokeResult;
}

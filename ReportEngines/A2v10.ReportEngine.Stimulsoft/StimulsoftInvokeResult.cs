// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

using A2v10.Infrastructure;

namespace A2v10.ReportEngine.Stimulsoft
{
	public record StimulsoftInvokeResult : IInvokeResult
	{
		public Byte[] Body { get; init; }
		public String ContentType { get; init; }
		public String FileName { get; init; }
	}
}

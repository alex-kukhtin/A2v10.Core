
using System;

using A2v10.Infrastructure;

namespace A2v10.Services
{
	public record InvokeResult : IInvokeResult
	{
		public Byte[] Body { get; init; }
		public String ContentType { get; init; }
		public String FileName { get; init; }
	}
}

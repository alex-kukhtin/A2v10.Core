
using System;

using A2v10.Infrastructure;

namespace A2v10.Services
{
	public class InvokeResult : IInvokeResult
	{
		public String Body { get; set; }

		public String ContentType { get; set; }
	}
}

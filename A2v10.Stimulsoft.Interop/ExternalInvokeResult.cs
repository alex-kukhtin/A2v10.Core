using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using A2v10.Infrastructure;

namespace A2v10.Stimulsoft.Interop
{
	public class ExternalInvokeResult : IInvokeResult
	{
		public Byte[] Body { get; init; }
		public String ContentType { get; init; }
	}
}

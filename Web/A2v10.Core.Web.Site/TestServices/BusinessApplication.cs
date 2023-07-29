using System;

namespace A2v10.Core.Web.Site.TestServices
{
	public record BusinessApplication
	{
		const String ERROR = "ERROR";
		public String Name { get; init; } = ERROR;
		public String Description { get; init; } = ERROR;
		public String Path { get; init; } = ERROR;
		public String Copyright { get; init; } = String.Empty;
		public Int32 Version { get; init; }
	}
}

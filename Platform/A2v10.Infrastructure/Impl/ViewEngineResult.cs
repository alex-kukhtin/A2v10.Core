
using System;

namespace A2v10.Infrastructure
{
	public class ViewEngineResult : IViewEngineResult
	{
		public IViewEngine Engine { get; init; }
		public String FileName { get; init; }
	}
}

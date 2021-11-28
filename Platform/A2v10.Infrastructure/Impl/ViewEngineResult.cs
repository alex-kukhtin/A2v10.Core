
using System;

namespace A2v10.Infrastructure
{
	public record ViewEngineResult : IViewEngineResult
	{
		public ViewEngineResult(IViewEngine engine, String fileName)
        {
			Engine = engine;
			FileName = fileName;	
        }
		public IViewEngine Engine { get; }
		public String FileName { get; }
	}
}

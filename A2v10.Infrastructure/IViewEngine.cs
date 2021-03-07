using A2v10.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Infrastructure
{
	public interface IRenderInfo
	{
		String RootId { get; }
		String FileName { get; }
		String FileTitle { get; }
		String Path { get; }
		String Text { get; }
		IDataModel DataModel { get; }
		//public ITypeChecker TypeChecker;
		String CurrentLocale { get; }
		Boolean SecondPhase { get; }
	}

	public interface IViewEngine
	{
		void Render(IRenderInfo info, TextWriter writer);
	}
}

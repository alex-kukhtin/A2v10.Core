// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.IO;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Xaml
{
	public class RenderInfo : IRenderInfo
	{
		public String RootId { get; init; }
		public String FileName { get; init; }
		public String FileTitle { get; init; }
		public String Path { get; init; }
		public String Text { get; init; }
		public IDataModel DataModel { get; init; }
		//public ITypeChecker TypeChecker
		public String CurrentLocale { get; init; }
		public Boolean IsDebugConfiguration { get; init; }
		public Boolean SecondPhase { get; init; }
	}

	public interface IRenderer
	{
		void Render(IRenderInfo info, TextWriter writer);
	}
}

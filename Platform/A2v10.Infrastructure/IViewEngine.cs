// Copyright © 2021-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;


public interface IRenderInfo
{
	String? RootId { get; }
	String? FileName { get; }
	String? FileTitle { get; }
	String Path { get; }
	String? Text { get; }
	IDataModel? DataModel { get; }
	String? CurrentLocale { get; }
	Boolean SecondPhase { get; }
	// Render="Debug" elements and TestId are written only under it
	Boolean IsDebugConfiguration { get; }
	ClientFramework Framework { get; }
}

public interface IRenderResult
{
	String Body { get; }
	String ContentType { get; }
}

public interface IViewEngine
{
	Task<IRenderResult> RenderAsync(IRenderInfo renderInfo);
}

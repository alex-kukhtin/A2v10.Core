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
	IDataModel? DataModel { get; }
	String? CurrentLocale { get; }
	Boolean SecondPhase { get; }
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

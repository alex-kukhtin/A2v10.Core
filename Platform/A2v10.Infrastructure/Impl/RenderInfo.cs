// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Data.Interfaces;

namespace A2v10.Infrastructure;

public class RenderInfo : IRenderInfo
{
	public String? RootId { get; init; }
	public String? FileName { get; init; }
	public String? FileTitle { get; init; }
	public String Path { get; init; } = String.Empty;
	public String? Text { get; init; }
	public IDataModel? DataModel { get; init; }
	//public ITypeChecker TypeChecker
	public String? CurrentLocale { get; init; }
	public Boolean IsDebugConfiguration { get; init; }
	public Boolean SecondPhase { get; init; }
}


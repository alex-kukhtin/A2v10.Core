// Copyright © 2021-2023 Olekdsandr Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure;

public record ViewEngineResult : IViewEngineResult
{
	public ViewEngineResult(IViewEngine engine, String path, String fileName)
        {
		Engine = engine;
		FileName = fileName;
		FilePath = path;
        }
	public IViewEngine Engine { get; }
	public String FileName { get; }
	public String FilePath { get; }
}

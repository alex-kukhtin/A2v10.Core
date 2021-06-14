﻿// Copyright © 2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure
{

	public interface IViewEngineResult {
		IViewEngine Engine { get; }
		String FileName { get; }
	}

	public interface IViewEngineProvider
	{
		IViewEngineResult FindViewEngine(String fileName);
		void RegisterEngine(String extension, Type engineType);
	}
}

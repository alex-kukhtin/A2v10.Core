// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;

namespace A2v10.Infrastructure
{
	public interface ITheme
	{
		String Name { get; }
		String FileName { get; }
		String ColorScheme { get; }
	}


	public interface IApplicationTheme
	{
		ITheme Theme { get; }
	}
}

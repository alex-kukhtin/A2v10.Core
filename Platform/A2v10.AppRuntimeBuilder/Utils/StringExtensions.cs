// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;

namespace A2v10.AppRuntimeBuilder;

internal static class StringExtensions
{
	public static String Singular(this String s)
	{
		if (s.EndsWith("ies"))
			return s[0..^3] + "y";
		else if (s.EndsWith('s'))
			return s[0..^1];
		return s;
	}
}

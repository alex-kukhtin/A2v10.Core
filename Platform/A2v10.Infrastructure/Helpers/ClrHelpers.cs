// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Text.RegularExpressions;

namespace A2v10.Infrastructure;

public static partial class ClrHelpers
{
	const String PATTERN = @"^\s*clr-type\s*:\s*([\w\.]+)\s*;\s*assembly\s*=\s*([\w\.]+)\s*$";
#if NET7_0_OR_GREATER
	[GeneratedRegex(PATTERN, RegexOptions.None, "en-US")]
	private static partial Regex IsClrRegex();
#else
	private static Regex CLRREGEX => new(PATTERN, RegexOptions.Compiled);
	private static Regex IsClrRegex() => CLRREGEX;
#endif
    public static Boolean IsClrPath(String? clrType)
	{
		if (clrType == null)
			return false;
        var match = IsClrRegex().Match(clrType);
        return match.Success && match.Groups.Count == 3;
    }
    public static (String assembly, String type) ParseClrType(String clrType)
	{
		var match = IsClrRegex().Match(clrType);
		if (match.Groups.Count != 3)
		{
			String errorMsg = $"Invalid clrType definition: '{clrType}'. Expected: 'clr-type:TypeName;assembly=AssemblyName'";
			throw new ArgumentException(errorMsg);
		}
		String assemblyName = match.Groups[2].Value.Trim();
		String typeName = match.Groups[1].Value.Trim();
		return (assemblyName, typeName);
	}
}

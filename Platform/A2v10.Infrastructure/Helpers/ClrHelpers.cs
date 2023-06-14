// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Text.RegularExpressions;

namespace A2v10.Infrastructure;

public static class ClrHelpers
{
    const String PATTERN = @"^\s*clr-type\s*:\s*([\w\.]+)\s*;\s*assembly\s*=\s*([\w\.]+)\s*$";
    public static Boolean IsClrPath(String? clrType)
	{
		if (clrType == null)
			return false;
        var match = Regex.Match(clrType, PATTERN);
        return match.Success && match.Groups.Count == 3;
    }
    public static (String assembly, String type) ParseClrType(String clrType)
	{
		var match = Regex.Match(clrType, PATTERN);
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

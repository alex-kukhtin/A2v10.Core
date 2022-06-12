
using System;
using System.Text.RegularExpressions;

namespace A2v10.Infrastructure
{
	public static class ClrHelpers
	{
		public static (String assembly, String type) ParseClrType(String clrType)
		{
			const String _pattern = @"^\s*clr-type\s*:\s*([\w\.]+)\s*;\s*assembly\s*=\s*([\w\.]+)\s*$";
			var match = Regex.Match(clrType, _pattern);
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
}

// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using A2v10.Infrastructure;

namespace A2v10.AppRuntimeBuilder;

internal static class EndpointExtensions
{
	public static String RealName(this EndpointDescriptor endpoint)
	{
		if (!String.IsNullOrEmpty(endpoint.Title))
			return endpoint.Title;
		var spl = endpoint.Name.Split('/');
		return $"@[{spl[^1].ToPascalCase()}]";
	}

	public static IEnumerable<(String Name, RuntimeTable Table)> AllReferences(this EndpointDescriptor endpoint)
	{
		if (endpoint.Metadata ==  null)
			yield break;
		var md = endpoint.Metadata;
		foreach (var c in md.Catalogs)
			foreach (var f in c.ReferenceTable(endpoint.Table, md))
				yield return f;
		foreach (var d in md.Documents)
			foreach (var f in d.ReferenceTable(endpoint.Table, md))
				yield return f;
		// Journals not needed
	}
}

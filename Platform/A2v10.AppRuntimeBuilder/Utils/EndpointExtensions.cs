// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using System;

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
}

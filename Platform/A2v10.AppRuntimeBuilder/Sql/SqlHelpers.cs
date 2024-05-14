// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;

using A2v10.Data;

namespace A2v10.AppRuntimeBuilder;

internal static class SqlHelpers
{
	public static Object GetDateParameter(ExpandoObject? eo, String name)
	{
		return eo.GetDateParameter(name);
	}

}

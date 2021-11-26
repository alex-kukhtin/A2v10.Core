// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.Dynamic;

namespace A2v10.Identity.Core.Helpers;
public static class ExpandoObjectExtensions
{
	public static ExpandoObject Add(this ExpandoObject obj, String name, Object value)
	{
		if (obj is not IDictionary<String, Object?> d)
			return obj;
		d.Add(name, value);
		return obj;
	}
}


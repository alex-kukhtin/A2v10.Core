// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;

using A2v10.Infrastructure;

namespace A2v10.Identity.UI;

public static class CodeProviderExtensions
{
	// @Html.Raw
	public static String AppStyleSheetsLink(this IAppCodeProvider provider)
	{
		var files = provider.EnumerateAllFiles("_assets", "*.css");
		// at least one file
		if (files != null && files.Any())
			return $"<link  href=\"/_shell/appstyles\" rel=\"stylesheet\" />";
		return String.Empty;
	}
}

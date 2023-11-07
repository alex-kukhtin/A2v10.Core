// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

using System;

using A2v10.Web.Identity;

namespace A2v10.Identity.UI;

public static class AppUserStoreOptionsHelpers
{
	// @Html.Raw
	public static String SecuritySchema(this AppUserStoreOptions<Int64> opts)
	{
		return opts.Schema ?? "a2security";
	}
}

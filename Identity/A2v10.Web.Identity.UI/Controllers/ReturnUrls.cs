// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;

using Microsoft.AspNetCore.Mvc;

namespace A2v10.Identity.UI;

public static class ReturnUrls
{
	/* Where a sign-in sends the browser: a local address of this site only - IsLocalUrl refuses
	 * 'https://evil', '//evil', '/\evil' and 'javascript:' - and never back into /account.
	 */
	public static String LocalOrRoot(IUrlHelper url, String? returnUrl)
		=> !String.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl)
			&& !returnUrl.StartsWith("/account", StringComparison.OrdinalIgnoreCase)
			? returnUrl
			: "/";
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Identity;

using A2v10.Web.Identity;

namespace A2v10.Identity.UI;

public static class EmailCodes
{
	/* A code from a letter is checked as a password is: a locked user is refused before the check, a
	 * wrong code is a failed attempt - MaxFailedAccessAttempts of them lock the user, password sign-in
	 * included - and a right one clears the count. Checked bare, six digits were enumerable within the
	 * code's life, and a reset code is the account.
	 */
	public static async Task<Boolean> VerifyAsync(UserManager<AppUser<Int64>> userManager, AppUser<Int64> user, String? code)
	{
		if (await userManager.IsLockedOutAsync(user))
			return false;
		if (!String.IsNullOrEmpty(code) && await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider, code))
		{
			await userManager.ResetAccessFailedCountAsync(user);
			return true;
		}
		await userManager.AccessFailedAsync(user);
		return false;
	}
}

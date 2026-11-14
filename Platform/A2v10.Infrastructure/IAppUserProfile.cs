// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

/* What a user writes in his own account, beside IAppUserAdmin, which acts on someone else's. The
 * columns ride in the sign-in's claims (the name in the header, the locale, the theme), so a write
 * refreshes the sign-in - the reason it is Identity's and not a caller's SQL.
 * The user is the current one, never an argument. null leaves the column as it is. A failure throws.
 */
public interface IAppUserProfile
{
	Task SetPersonalAsync(String? personName, String? locale, String? theme);
}

// a2security.Users.Theme, nchar(1): what IApplicationTheme reads and the profile offers
public static class UserTheme
{
	public const String Light = "L";
	public const String Dark = "D";
	// the browser's choice, light or dark; the column's default
	public const String Auto = "A";
}

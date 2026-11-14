// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Identity;

using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Identity.UI;

internal sealed class AppUserProfile(UserManager<AppUser<Int64>> _userManager, SignInManager<AppUser<Int64>> _signInManager,
	ICurrentUser _currentUser) : IAppUserProfile
{
	public async Task SetPersonalAsync(String? personName, String? locale, String? theme)
	{
		var id = _currentUser.Identity.Id ?? throw new InvalidOperationException("UserId is null");
		var user = await _userManager.FindByIdAsync(id.ToString());
		// the store answers an unknown id with an empty user, not null
		if (user == null || user.IsEmpty)
			throw new InvalidOperationException($"User '{id}' not found");
		if (personName != null)
		{
			user.PersonName = personName;
			user.Flags |= UpdateFlags.PersonName;
		}
		if (locale != null)
		{
			user.Locale = locale;
			user.Flags |= UpdateFlags.Locale;
		}
		if (theme != null)
		{
			user.Theme = theme;
			user.Flags |= UpdateFlags.Theme;
		}
		var result = await _userManager.UpdateAsync(user);
		if (!result.Succeeded)
			throw new InvalidOperationException(String.Join(", ", result.Errors.Select(e => e.Code)));
		await _signInManager.RefreshSignInAsync(user);
	}
}

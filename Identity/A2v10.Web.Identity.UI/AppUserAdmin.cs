// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Web.Identity;

namespace A2v10.Identity.UI;

// Single-tenant: unlike the clr handlers it writes no tenant segment - its caller, the metadata layer, has none.
internal sealed class AppUserAdmin(IServiceProvider _serviceProvider, UserManager<AppUser<Int64>> _userManager,
	AppUserStore<Int64> _userStore, IDbContext _dbContext, IOptions<AppUserStoreOptions<Int64>> _userStoreOptions,
	ICurrentUser _currentUser) : IAppUserAdmin
{
	public async Task<Int64> CreateAsync(String userName, String password)
	{
		var user = new AppUser<Int64>() { UserName = userName, Email = userName };
		Check(await _userManager.CreateAsync(user, password));
		var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
		Check(await _userManager.ConfirmEmailAsync(user, token));
		return user.Id;
	}

	public async Task<Int64> InviteAsync(String userName)
	{
		var user = new AppUser<Int64>() { UserName = userName, Email = userName };
		Check(await _userManager.CreateAsync(user));
		await new EmailSender(_serviceProvider).SendInviteEMail(user);
		return user.Id;
	}

	public Task InviteAgainAsync(Int64 id)
	{
		return new EmailSender(_serviceProvider).SendInviteAgain(id);
	}

	public async Task SetPasswordAsync(Int64 id, String password)
	{
		var user = await FindAsync(id);
		var token = await _userManager.GeneratePasswordResetTokenAsync(user);
		Check(await _userManager.ResetPasswordAsync(user, token, password));
	}

	// [User.SetTwoFactorEnabled] drops the authenticator key with the flag; the manager rotates the stamp.
	public async Task ResetTwoFactorAsync(Int64 id)
	{
		Check(await _userManager.SetTwoFactorEnabledAsync(await FindAsync(id), false));
	}

	/* The store reports a block as a lockout with no end, which every sign-in already checks; a live
	 * session is cut by the stamp, which the cookie validator compares and the lockout does not.
	 */
	public async Task SetBlockedAsync(Int64 id, Boolean blocked)
	{
		var user = await FindAsync(id);
		await _userStore.SetBlockedAsync(user, blocked);
		if (blocked)
			Check(await _userManager.UpdateSecurityStampAsync(user));
	}

	public Task DeleteAsync(Int64 id)
	{
		var options = _userStoreOptions.Value;
		var prms = new DeleteUserParams()
		{
			UserId = _currentUser.Identity.Id ?? throw new InvalidOperationException("CurrentUser is null"),
			Id = id
		};
		return _dbContext.ExecuteAsync(options.DataSource, $"[{options.SecuritySchema}].[User.DeleteUser]", prms);
	}

	async Task<AppUser<Int64>> FindAsync(Int64 id)
	{
		var user = await _userManager.FindByIdAsync(id.ToString());
		// the store answers an unknown id with an empty user, not null
		if (user == null || user.IsEmpty)
			throw new InvalidOperationException($"User '{id}' not found");
		return user;
	}

	static void Check(IdentityResult result)
	{
		if (!result.Succeeded)
			throw new InvalidOperationException(String.Join(", ", result.Errors.Select(e => e.Code)));
	}
}

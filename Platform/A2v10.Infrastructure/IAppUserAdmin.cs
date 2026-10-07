// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

namespace A2v10.Infrastructure;

/* What an admin does to someone else's account. Only operations with an Identity invariant behind them
 * (password hash, security stamp, the invitation mail, the authenticator key, the deleted-users archive):
 * the caller holds no Identity reference and must not write those columns. The profile and the roles
 * carry no such invariant - the caller writes them with its own SQL.
 * The acting user is the current one, never an argument. A failure throws.
 */
public interface IAppUserAdmin
{
	// The email is the user name and is confirmed: the admin vouches for the address.
	Task<Int64> CreateAsync(String userName, String password);
	Task<Int64> InviteAsync(String userName);
	Task InviteAgainAsync(Int64 id);
	Task SetPasswordAsync(Int64 id, String password);
	// For a user who lost the authenticator and cannot sign in to reset it from the profile.
	Task ResetTwoFactorAsync(Int64 id);
	Task SetBlockedAsync(Int64 id, Boolean blocked);
	Task DeleteAsync(Int64 id);
}

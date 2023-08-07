// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Threading;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Identity;


namespace A2v10.Web.Identity;

public sealed class AppRoleStore<T> : IRoleStore<AppRole<T>> where T : struct
{
	public Task<IdentityResult> CreateAsync(AppRole<T> role, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task<IdentityResult> DeleteAsync(AppRole<T> role, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public void Dispose()
	{
	}


	[return : NotNull]
#if NET6_0
	public Task<AppRole<T>> FindByIdAsync(String roleId, CancellationToken cancellationToken)
#elif NET7_0_OR_GREATER
	public Task<AppRole<T>?> FindByIdAsync(String roleId, CancellationToken cancellationToken)
#endif
	{
		throw new NotImplementedException();
	}

	[return: NotNull]
#if NET6_0
	public Task<AppRole<T>> FindByNameAsync(String normalizedRoleName, CancellationToken cancellationToken)
#elif NET7_0_OR_GREATER
	public Task<AppRole<T>?> FindByNameAsync(String normalizedRoleName, CancellationToken cancellationToken)
#endif
	{
		throw new NotImplementedException();
	}

	public Task<String?> GetNormalizedRoleNameAsync(AppRole<T> role, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task<String> GetRoleIdAsync(AppRole<T> role, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task<String?> GetRoleNameAsync(AppRole<T> role, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task SetNormalizedRoleNameAsync(AppRole<T> role, String? normalizedName, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task SetRoleNameAsync(AppRole<T> role, String? roleName, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task<IdentityResult> UpdateAsync(AppRole<T> role, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
}


using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using A2v10.Data.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace A2v10.Web.Identity
{
	public sealed class AppUserStore : IUserStore<AppUser>, IUserLoginStore<AppUser>,
		IUserSecurityStampStore<AppUser>
	{
		private readonly IDbContext _dbContext;

		public AppUserStore(IDbContext dbContext)
		{
			_dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
		}

		public Task AddLoginAsync(AppUser user, UserLoginInfo login, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<IdentityResult> CreateAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<IdentityResult> DeleteAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}

		public Task<AppUser> FindByIdAsync(String userId, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<AppUser> FindByLoginAsync(String loginProvider, String providerKey, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<AppUser> FindByNameAsync(String normalizedUserName, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<IList<UserLoginInfo>> GetLoginsAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<String> GetNormalizedUserNameAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<String> GetSecurityStampAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<String> GetUserIdAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<String> GetUserNameAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task RemoveLoginAsync(AppUser user, String loginProvider, String providerKey, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task SetNormalizedUserNameAsync(AppUser user, String normalizedName, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task SetSecurityStampAsync(AppUser user, String stamp, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task SetUserNameAsync(AppUser user, String userName, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}
	}
}

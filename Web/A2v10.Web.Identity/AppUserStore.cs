using System;
using System.Collections.Generic;
using System.Text;
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

		public Task<AppUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<AppUser> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<AppUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<IList<UserLoginInfo>> GetLoginsAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<string> GetNormalizedUserNameAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<string> GetSecurityStampAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<string> GetUserIdAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<string> GetUserNameAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task RemoveLoginAsync(AppUser user, string loginProvider, string providerKey, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task SetNormalizedUserNameAsync(AppUser user, string normalizedName, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task SetSecurityStampAsync(AppUser user, string stamp, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task SetUserNameAsync(AppUser user, string userName, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}
	}
}

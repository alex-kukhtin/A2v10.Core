// Copyright © 2015-2021 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Identity;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Web.Identity
{
	public sealed class AppUserStore :
		IUserStore<AppUser>,
		IUserLoginStore<AppUser>,
		IUserEmailStore<AppUser>,
		IUserPasswordStore<AppUser>,
		IUserSecurityStampStore<AppUser>,
		IUserClaimStore<AppUser>,
		IUserAuthenticationTokenStore<AppUser>
	{
		private readonly IDbContext _dbContext;

		private String DataSource { get; } 
		private String DbSchema { get; }

		public AppUserStore(IDbContext dbContext, AppUserStoreOptions options)
		{
			_dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
			DataSource = options.DataSource;
			DbSchema = options.Schema;
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
			// do nothing?
		}

		public Task<AppUser> FindByIdAsync(String UserId, CancellationToken cancellationToken)
		{
			var Id = Int64.Parse(UserId);
			return _dbContext.LoadAsync<AppUser>(DataSource, $"[{DbSchema}].[FindUserById]", new { Id });
		}

		public Task<AppUser> FindByNameAsync(String normalizedUserName, CancellationToken cancellationToken)
		{
			var UserName = normalizedUserName.ToLowerInvariant(); // A2v10
			return _dbContext.LoadAsync<AppUser>(DataSource, $"[{DbSchema}].[FindUserByName]", new { UserName });
		}

		public Task<String> GetNormalizedUserNameAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<String> GetUserIdAsync(AppUser user, CancellationToken cancellationToken)
		{
			return Task.FromResult<String>(user.Id.ToString());
		}

		public Task<String> GetUserNameAsync(AppUser user, CancellationToken cancellationToken)
		{
			return Task.FromResult<String>(user.UserName);
		}

		public Task SetNormalizedUserNameAsync(AppUser user, String normalizedName, CancellationToken cancellationToken)
		{
			user.UserName = normalizedName.ToLowerInvariant();
			return Task.CompletedTask;
		}

		public Task SetUserNameAsync(AppUser user, String userName, CancellationToken cancellationToken)
		{
			user.UserName = userName;
			return Task.CompletedTask;
		}

		public Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken cancellationToken)
		{
			//throw new NotImplementedException();
			// TODO: Update user
			return Task.FromResult<IdentityResult>(IdentityResult.Success);
		}

		#region IUserSecurityStampStore
		public Task<String> GetSecurityStampAsync(AppUser user, CancellationToken cancellationToken)
		{
			// .net framework compatibility
			return Task.FromResult<String>(user.SecurityStamp2 ?? user.SecurityStamp);
		}

		public async Task SetSecurityStampAsync(AppUser user, String stamp, CancellationToken cancellationToken)
		{
			var prm = new ExpandoObject()
			{
				{ "UserId",  user.Id },
				{ "SecurityStamp",  stamp }
			};
			await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.SetSecurityStamp]", prm);
			user.SecurityStamp2 = stamp;
		}
		#endregion

		#region IUserEmailStore
		public Task SetEmailAsync(AppUser user, String email, CancellationToken cancellationToken)
		{
			user.Email = email;
			return Task.CompletedTask;
		}

		public Task<string> GetEmailAsync(AppUser user, CancellationToken cancellationToken)
		{
			return Task.FromResult<String>(user.Email);
		}

		public Task<Boolean> GetEmailConfirmedAsync(AppUser user, CancellationToken cancellationToken)
		{
			return Task.FromResult<Boolean>(user.EmailConfirmed);
		}

		public Task SetEmailConfirmedAsync(AppUser user, Boolean confirmed, CancellationToken cancellationToken)
		{
			user.EmailConfirmed = confirmed;
			return Task.CompletedTask;
		}

		public Task<AppUser> FindByEmailAsync(String normalizedEmail, CancellationToken cancellationToken)
		{
			var Email = normalizedEmail.ToLowerInvariant(); // A2v10
			return _dbContext.LoadAsync<AppUser>(DataSource, $"[{DbSchema}].[FindUserByEmail]", new { Email });
		}

		public Task<string> GetNormalizedEmailAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task SetNormalizedEmailAsync(AppUser user, String normalizedEmail, CancellationToken cancellationToken)
		{
			user.Email = normalizedEmail.ToLowerInvariant();
			return Task.CompletedTask;
		}
		#endregion

		#region IUserPasswordStore
		public async Task SetPasswordHashAsync(AppUser user, String passwordHash, CancellationToken cancellationToken)
		{
			var prm = new ExpandoObject()
			{
				{ "UserId",  user.Id },
				{ "PasswordHash",  passwordHash }
			};
			await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.SetPasswordHash]", prm);
			user.PasswordHash2 = passwordHash;

		}

		public Task<String> GetPasswordHashAsync(AppUser user, CancellationToken cancellationToken)
		{
			// .net framework compatibility
			return Task.FromResult<String>(user.PasswordHash2 ?? user.PasswordHash);
		}

		public Task<bool> HasPasswordAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}
		#endregion

		#region IUserClaimStore
		public Task<IList<Claim>> GetClaimsAsync(AppUser user, CancellationToken cancellationToken)
		{
			List<Claim> list = new()
			{
				new Claim(WellKnownClims.PersonName, user.PersonName ?? String.Empty),
			};

			if (user.Tenant != 0)
			{
				list.Add(new Claim(WellKnownClims.TenantId, user.Tenant.ToString()));
				//if (user.IsTenantAdmin)
					//list.Add(new Claim("TenantAdmin", "TenantAdmin"));
			}
			if (!String.IsNullOrEmpty(user.Segment))
				list.Add(new Claim(WellKnownClims.Segment, user.Segment));
			if (!String.IsNullOrEmpty(user.Locale))
				list.Add(new Claim(WellKnownClims.Locale, user.Locale));

			//if (user.IsAdmin) // TODO
			list.Add(new Claim(WellKnownClims.Admin, "Admin"));
			/*
			if (_host.IsMultiTenant)
			{
				var clientId = user.GetClientId();
				if (clientId != null)
					list.Add(new Claim("ClientId", clientId));
				if (user.IsTenantAdmin)
					list.Add(new Claim("TenantAdmin", "TenantAdmin"));
			}
			*/
			return Task.FromResult<IList<Claim>>(list);
		}

		public Task AddClaimsAsync(AppUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
		{
			throw new NotImplementedException(nameof(AddClaimsAsync));
		}

		public Task ReplaceClaimAsync(AppUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
		{
			throw new NotImplementedException(nameof(ReplaceClaimAsync));
		}

		public Task RemoveClaimsAsync(AppUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<IList<AppUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}
		#endregion

		#region IUserLoginStore
		public Task AddLoginAsync(AppUser user, UserLoginInfo login, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<AppUser> FindByLoginAsync(String loginProvider, String providerKey, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<IList<UserLoginInfo>> GetLoginsAsync(AppUser user, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task RemoveLoginAsync(AppUser user, String loginProvider, String providerKey, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region IUserAuthenticationTokenStore
		public async Task SetTokenAsync(AppUser user, String loginProvider, String name, String value, CancellationToken cancellationToken)
		{
			var prm = new ExpandoObject()
			{
				{ "UserId",  user.Id },
				{ "Provider",  loginProvider},
				{ "Name",  name},
				{ "Value",  value}
			};
			await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.SetToken]", prm);
		}

		public Task RemoveTokenAsync(AppUser user, String loginProvider, String name, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task<String> GetTokenAsync(AppUser user, String loginProvider, String name, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}
		#endregion

	}
}

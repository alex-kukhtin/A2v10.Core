// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.Dynamic;
using System.Security.Claims;
using System.Threading;

using Microsoft.AspNetCore.Identity;

using A2v10.Data.Interfaces;
using A2v10.Identity.Core.Helpers;

namespace A2v10.Web.Identity;
public sealed class AppUserStore :
	IUserStore<AppUser>,
	IUserLoginStore<AppUser>,
	IUserEmailStore<AppUser>,
	IUserPhoneNumberStore<AppUser>,
	IUserPasswordStore<AppUser>,
	IUserSecurityStampStore<AppUser>,
	IUserClaimStore<AppUser>
{
	private readonly IDbContext _dbContext;

	private String? DataSource { get; } 
	private String DbSchema { get; }

	public AppUserStore(IDbContext dbContext, AppUserStoreOptions options)
	{
		_dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
		DataSource = options.DataSource;
		DbSchema = options.Schema;
	}

	public async Task<IdentityResult> CreateAsync(AppUser user, CancellationToken cancellationToken)
	{
		if (user.PasswordHash == null)
			user.PasswordHash = user.PasswordHash2;
		if (user.SecurityStamp == null)
			user.SecurityStamp = user.SecurityStamp2;

		await _dbContext.ExecuteAsync<AppUser>(DataSource, $"[{DbSchema}].[CreateUser]", user);
		return IdentityResult.Success;
	}

	public Task<IdentityResult> DeleteAsync(AppUser user, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public void Dispose()
	{
		// do nothing?
	}

	public async Task<AppUser> FindByIdAsync(String UserId, CancellationToken cancellationToken)
	{
		var Id = Int64.Parse(UserId);
		return await _dbContext.LoadAsync<AppUser>(DataSource, $"[{DbSchema}].[FindUserById]", new { Id }) 
			?? throw new IdentityCoreException($"User not found (Id='{UserId}')");
	}

	public async Task<AppUser> FindByNameAsync(String normalizedUserName, CancellationToken cancellationToken)
	{
		var UserName = normalizedUserName.ToLowerInvariant(); // A2v10
		return await _dbContext.LoadAsync<AppUser>(DataSource, $"[{DbSchema}].[FindUserByName]", new { UserName })
			?? throw new IdentityCoreException($"User not found (Name='{UserName}')");
	}

	public Task<String> GetNormalizedUserNameAsync(AppUser user, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task<String> GetUserIdAsync(AppUser user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String>(user.Id.ToString());
	}

	public Task<String?> GetUserNameAsync(AppUser user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String?>(user.UserName);
	}

	public Task SetNormalizedUserNameAsync(AppUser user, String normalizedName, CancellationToken cancellationToken)
	{
		user.UserName = normalizedName?.ToLowerInvariant();
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
		return Task.FromResult<String>(user.SecurityStamp2 ?? user.SecurityStamp ?? String.Empty);
	}

	public async Task SetSecurityStampAsync(AppUser user, String stamp, CancellationToken cancellationToken)
	{
		if (user.Id != 0) {
			var prm = new ExpandoObject()
			{
				{ "UserId",  user.Id },
				{ "SecurityStamp",  stamp }
			};
			await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.SetSecurityStamp]", prm);
		}
		user.SecurityStamp2 = stamp;
	}
	#endregion

	#region IUserEmailStore
	public Task SetEmailAsync(AppUser user, String email, CancellationToken cancellationToken)
	{
		user.Email = email;
		return Task.CompletedTask;
	}

	public Task<String?> GetEmailAsync(AppUser user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String?>(user.Email);
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

	public async Task<AppUser> FindByEmailAsync(String normalizedEmail, CancellationToken cancellationToken)
	{
		var Email = normalizedEmail?.ToLowerInvariant(); // A2v10
		return await _dbContext.LoadAsync<AppUser>(DataSource, $"[{DbSchema}].[FindUserByEmail]", new { Email })
			?? throw new IdentityCoreException($"User not found (Emali='{Email}')");
	}

	public Task<String> GetNormalizedEmailAsync(AppUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<String>(user.Email?.ToLowerInvariant() ?? String.Empty);
	}

	public Task SetNormalizedEmailAsync(AppUser user, String normalizedEmail, CancellationToken cancellationToken)
	{
		user.Email = normalizedEmail?.ToLowerInvariant();
		return Task.CompletedTask;
	}
	#endregion

	#region IUserPasswordStore
	public async Task SetPasswordHashAsync(AppUser user, String passwordHash, CancellationToken cancellationToken)
	{
		if (user.Id != 0)
		{
			var prm = new ExpandoObject()
			{
				{ "UserId",  user.Id },
				{ "PasswordHash",  passwordHash }
			};
			await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.SetPasswordHash]", prm);
		}
		user.PasswordHash2 = passwordHash;
	}

	public Task<String> GetPasswordHashAsync(AppUser user, CancellationToken cancellationToken)
	{
		// .net framework compatibility
		return Task.FromResult<String>(user.PasswordHash2 ?? user.PasswordHash ?? String.Empty);
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

	#region IUserPhoneNumberStore
	public Task SetPhoneNumberAsync(AppUser user, String phoneNumber, CancellationToken cancellationToken)
    {
		return Task.CompletedTask;
    }

	public Task<String> GetPhoneNumberAsync(AppUser user, CancellationToken cancellationToken)
    {
		return Task.FromResult(user.PhoneNumber ?? throw new InvalidOperationException("Phone number is null"));
    }

	public Task<Boolean> GetPhoneNumberConfirmedAsync(AppUser user, CancellationToken cancellationToken)
    {
		return Task.FromResult(user.PhoneNumberConfirmed);
    }

	public async Task SetPhoneNumberConfirmedAsync(AppUser user, Boolean confirmed, CancellationToken cancellationToken)
    {
		var prm = new ExpandoObject()
		{
			{ "UserId",  user.Id },
			{ "Confirmed",  confirmed}
		};
		await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.SetPhoneNumberConfirmed]", prm);
    }
	#endregion

	#region Token support
	public Task AddTokenAsync(AppUser user, String provider, String token, DateTime expires, String? tokenToRemove = null)
	{
		var exp = new ExpandoObject()
		{
			{ "UserId", user.Id },
			{ "Provider", provider },
			{ "Token", token },
			{ "Expires", expires }
		};
		if (!String.IsNullOrEmpty(tokenToRemove))
			exp.Add("Remove", tokenToRemove);
		return _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].AddToken", exp);
	}

	public async Task<String?> GetTokenAsync(AppUser user, String provider, String token)
	{
		var exp = new ExpandoObject()
		{
			{ "UserId", user.Id },
			{ "Provider", provider },
			{ "Token", token }
		};
		var res = await _dbContext.LoadAsync<JwtToken>(DataSource, $"[{DbSchema}].GetToken", exp);
		return res?.Token;
	}

	public Task RemoveTokenAsync(AppUser user, String provider, String token)
	{
		var exp = new ExpandoObject()
		{
			{ "UserId", user.Id },
			{ "Provider", provider },
			{ "Token", token }
		};
		return _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].RemoveToken", exp);
	}
	#endregion
}

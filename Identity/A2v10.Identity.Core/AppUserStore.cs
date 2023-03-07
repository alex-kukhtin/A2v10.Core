// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System.Collections.Generic;
using System.Dynamic;
using System.Security.Claims;
using System.Threading;
using System.ComponentModel;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using A2v10.Data.Interfaces;
using A2v10.Identity.Core.Helpers;

namespace A2v10.Web.Identity;

public sealed class AppUserStore<T>:
	IUserStore<AppUser<T>>,
	IUserLoginStore<AppUser<T>>,
	IUserEmailStore<AppUser<T>>,
	IUserPhoneNumberStore<AppUser<T>>,
	IUserPasswordStore<AppUser<T>>,
	IUserSecurityStampStore<AppUser<T>>,
	IUserClaimStore<AppUser<T>> where T : struct
{
	private readonly IDbContext _dbContext;
	private String? DataSource { get; } 
	private String DbSchema { get; }
	private readonly Func<AppUser<T>, IEnumerable<KeyValuePair<String, String?>>>? _addClaims;

	private static class ParamNames
    {
		public const String Id = nameof(Id);
		public const String Provider = nameof(Provider);
		public const String Token = nameof(Token);
		public const String PasswordHash = nameof(PasswordHash);
		public const String SecurityStamp = nameof(SecurityStamp);
		public const String Confirmed = nameof(Confirmed);
		public const String Expires = nameof(Expires);
	}
	public AppUserStore(IDbContext dbContext, IOptions<AppUserStoreOptions<T>> options)
	{
		_dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
		DataSource = options.Value?.DataSource;
		DbSchema = options.Value?.Schema ?? "a2security";
		_addClaims = options.Value?.Claims;
    }

    public async Task<IdentityResult> CreateAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		user.PasswordHash ??= user.PasswordHash2;
		user.SecurityStamp ??= user.SecurityStamp2;

		await _dbContext.ExecuteAsync<AppUser<T>>(DataSource, $"[{DbSchema}].[CreateUser]", user);
		return IdentityResult.Success;
	}

	public async Task<IdentityResult> DeleteAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		await _dbContext.ExecuteAsync<AppUser<T>>(DataSource, $"[{DbSchema}].[DeleteUser]", user);
		return IdentityResult.Success;
	}

	public void Dispose()
	{
		// do nothing?
	}

	public async Task<AppUser<T>> FindByIdAsync(String UserId, CancellationToken cancellationToken)
	{
		T? typedUserId = (T?) TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(UserId);
		if (typedUserId == null)
			return new AppUser<T>();
		return await _dbContext.LoadAsync<AppUser<T>>(DataSource, $"[{DbSchema}].[FindUserById]", new { Id = typedUserId })
			?? new AppUser<T>();
	}

	public async Task<AppUser<T>> FindByNameAsync(String normalizedUserName, CancellationToken cancellationToken)
	{
		var UserName = normalizedUserName.ToLowerInvariant(); // A2v10
		var user = await _dbContext.LoadAsync<AppUser<T>>(DataSource, $"[{DbSchema}].[FindUserByName]", new { UserName });
		return user ?? new AppUser<T>();
	}

	public Task<String> GetNormalizedUserNameAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String>(user.UserName?.ToLowerInvariant() ?? String.Empty);
	}

	public Task<String> GetUserIdAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String>(user.Id.ToString()!);
	}

	public Task<String?> GetUserNameAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String?>(user.UserName);
	}

	public Task SetNormalizedUserNameAsync(AppUser<T> user, String normalizedName, CancellationToken cancellationToken)
	{
		user.UserName = normalizedName?.ToLowerInvariant();
		return Task.CompletedTask;
	}

	public Task SetUserNameAsync(AppUser<T> user, String userName, CancellationToken cancellationToken)
	{
		user.UserName = userName;
		return Task.CompletedTask;
	}

	public Task<IdentityResult> UpdateAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		//throw new NotImplementedException();
		// TODO: Update user
		return Task.FromResult<IdentityResult>(IdentityResult.Success);
	}

	#region IUserSecurityStampStore
	public Task<String> GetSecurityStampAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		// .net framework compatibility
		return Task.FromResult<String>(user.SecurityStamp2 ?? user.SecurityStamp ?? String.Empty);
	}

	public async Task SetSecurityStampAsync(AppUser<T> user, String stamp, CancellationToken cancellationToken)
	{
		if (EqualityComparer<T>.Default.Equals(user.Id, default))
		{
			user.SecurityStamp2 = stamp;
			return;
		}
		var prm = new ExpandoObject()
		{
			{ ParamNames.Id,  user.Id },
			{ ParamNames.SecurityStamp,  stamp }
		};
		await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.SetSecurityStamp]", prm);
		user.SecurityStamp2 = stamp;
	}
	#endregion

	#region IUserEmailStore
	public Task SetEmailAsync(AppUser<T> user, String email, CancellationToken cancellationToken)
	{
		user.Email = email;
		return Task.CompletedTask;
	}

	public Task<String?> GetEmailAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String?>(user.Email);
	}

	public Task<Boolean> GetEmailConfirmedAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<Boolean>(user.EmailConfirmed);
	}

	public async Task SetEmailConfirmedAsync(AppUser<T> user, Boolean confirmed, CancellationToken cancellationToken)
	{
		var prm = new ExpandoObject()
		{
			{ ParamNames.Id,  user.Id },
			{ ParamNames.Confirmed,  confirmed}
		};
		await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.SetEMailConfirmed]", prm);
		user.EmailConfirmed = confirmed;
	}

	public async Task<AppUser<T>> FindByEmailAsync(String normalizedEmail, CancellationToken cancellationToken)
	{
		var Email = normalizedEmail?.ToLowerInvariant(); // A2v10
		return await _dbContext.LoadAsync<AppUser<T>>(DataSource, $"[{DbSchema}].[FindUserByEmail]", new { Email })
			?? new AppUser<T>();
	}

	public Task<String> GetNormalizedEmailAsync(AppUser<T> user, CancellationToken cancellationToken)
    {
        return Task.FromResult<String>(user.Email?.ToLowerInvariant() ?? String.Empty);
	}

	public Task SetNormalizedEmailAsync(AppUser<T> user, String normalizedEmail, CancellationToken cancellationToken)
	{
		user.Email = normalizedEmail?.ToLowerInvariant();
		return Task.CompletedTask;
	}
	#endregion

	#region IUserPasswordStore
	public async Task SetPasswordHashAsync(AppUser<T> user, String passwordHash, CancellationToken cancellationToken)
	{
		if (EqualityComparer<T>.Default.Equals(user.Id, default))
		{
			user.PasswordHash2 = passwordHash;
			return;
		}
		var prm = new ExpandoObject()
		{
			{ ParamNames.Id,  user.Id },
			{ ParamNames.PasswordHash,  passwordHash }
		};
		await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.SetPasswordHash]", prm);
		user.PasswordHash2 = passwordHash;
	}

	public Task<String> GetPasswordHashAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		// .net framework compatibility
		return Task.FromResult<String>(user.PasswordHash2 ?? user.PasswordHash ?? String.Empty);
	}

	public Task<bool> HasPasswordAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
	#endregion

	#region IUserClaimStore
	public Task<IList<Claim>> GetClaimsAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		List<Claim> list = new();
		if (_addClaims != null)
		{
			foreach (var (k, v) in _addClaims(user))
			{
				if (v != null)
					list.Add(new Claim(k, v));
			}
		}
		else
			AddDefaultClaims(user, list);
		return Task.FromResult<IList<Claim>>(list);
	}


	private static void AddDefaultClaims(AppUser<T> user, List<Claim> list)
	{
		list.Add(new Claim(WellKnownClaims.NameIdentifier, user.Id.ToString()!));
		list.Add(new Claim(WellKnownClaims.PersonName, user.PersonName ?? String.Empty));
		if (!String.IsNullOrEmpty(user.FirstName))
			list.Add(new Claim(WellKnownClaims.FirstName, user.FirstName));
		if (!String.IsNullOrEmpty(user.LastName))
			list.Add(new Claim(WellKnownClaims.LastName, user.LastName));
		if (user.Tenant != null)
		{
			list.Add(new Claim(WellKnownClaims.Tenant, user.Tenant.ToString()!));
			//if (user.IsTenantAdmin)
				//list.Add(new Claim("TenantAdmin", "TenantAdmin"));
		}
		if (!String.IsNullOrEmpty(user.Segment))
			list.Add(new Claim(WellKnownClaims.Segment, user.Segment));
		if (!String.IsNullOrEmpty(user.Locale))
			list.Add(new Claim(WellKnownClaims.Locale, user.Locale));
		if (user.Organization != null)
			list.Add(new Claim(WellKnownClaims.Organization, user.Organization.ToString()!));
		if (user.OrganizationKey != null)
			list.Add(new Claim(WellKnownClaims.OrganizationKey, user.OrganizationKey));
		if (user.IsPersistent)
			list.Add(new Claim(WellKnownClaims.IsPersistent, "true"));

		//if (user.IsAdmin) // TODO
		//list.Add(new Claim(WellKnownClims.Admin, "Admin"));
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
	}

	private static T? ConvertTo(String value)
	{
		return (T?) TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(value); 
	}

	private async Task UpdateClaim(AppUser<T> user, Claim claim)
	{
		var prm = new ExpandoObject()
		{
			{ ParamNames.Id,  user.Id },
			{ claim.Type,  claim.Value}
		};
		await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.Claim.{claim.Type}]", prm);

		switch (claim.Type)
		{
			case WellKnownClaims.Tenant:
				user.Tenant = ConvertTo(claim.Value);
				break;
			case WellKnownClaims.Organization:
				user.Organization = ConvertTo(claim.Value);
				break;
			case WellKnownClaims.Locale:
				user.Locale = claim.Value;
				break;
			case WellKnownClaims.PersonName:
				user.PersonName = claim.Value;
				break;
			case WellKnownClaims.FirstName:
				user.FirstName = claim.Value;
				break;
			case WellKnownClaims.LastName:
				user.LastName = claim.Value;
				break;
		}
	}

	public async Task AddClaimsAsync(AppUser<T> user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
	{
		// dynamically added 
		foreach (var claim in claims)
        {
			if (claim.Value == null)
				continue;
			await UpdateClaim(user, claim);
		}
	}



	public Task ReplaceClaimAsync(AppUser<T> user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
	{
		return UpdateClaim(user, claim);
	}

	public Task RemoveClaimsAsync(AppUser<T> user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task<IList<AppUser<T>>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
	#endregion

	#region IUserLoginStore
	public Task AddLoginAsync(AppUser<T> user, UserLoginInfo login, CancellationToken cancellationToken)
	{
		// ParamName: UserId
		throw new NotImplementedException();
	}

	public async Task<AppUser<T>> FindByLoginAsync(String loginProvider, String providerKey, CancellationToken cancellationToken)
	{
		if (loginProvider == "ApiKey")
		{
			var prms = new ExpandoObject()
			{
				{ "ApiKey", providerKey }
			};
			return await _dbContext.LoadAsync<AppUser<T>>(DataSource, $"[{DbSchema}].[FindApiUserByApiKey]", prms)
				?? new AppUser<T>();
		}
		else if (loginProvider == "PhoneNumber")
		{
			var prms = new ExpandoObject()
			{
				{ "PhoneNumber", providerKey }
			};
			return await _dbContext.LoadAsync<AppUser<T>>(DataSource, $"[{DbSchema}].[FindUserByPhoneNumber]", prms)
				?? new AppUser<T>();

		}
		else if (loginProvider == "Email")
		{
			return await FindByEmailAsync(providerKey, cancellationToken);
		}
		throw new NotImplementedException(loginProvider);
	}

	public Task<IList<UserLoginInfo>> GetLoginsAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task RemoveLoginAsync(AppUser<T> user, String loginProvider, String providerKey, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
	#endregion

	#region IUserPhoneNumberStore
	public Task SetPhoneNumberAsync(AppUser<T> user, String phoneNumber, CancellationToken cancellationToken)
    {
		return Task.CompletedTask;
    }

	public Task<String> GetPhoneNumberAsync(AppUser<T> user, CancellationToken cancellationToken)
    {
		return Task.FromResult(user.PhoneNumber ?? throw new InvalidOperationException("Phone number is null"));
    }

	public Task<Boolean> GetPhoneNumberConfirmedAsync(AppUser<T> user, CancellationToken cancellationToken)
    {
		return Task.FromResult(user.PhoneNumberConfirmed);
    }

	public async Task SetPhoneNumberConfirmedAsync(AppUser<T> user, Boolean confirmed, CancellationToken cancellationToken)
    {
		var prm = new ExpandoObject()
		{
			{ ParamNames.Id,  user.Id },
			{ ParamNames.Confirmed,  confirmed}
		};
		await _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].[User.SetPhoneNumberConfirmed]", prm);
		user.PhoneNumberConfirmed = confirmed;
    }
	#endregion

	#region Token support
	public Task AddTokenAsync(AppUser<T> user, String provider, String token, DateTime expires, String? tokenToRemove = null)
	{
		var exp = new ExpandoObject()
		{
			{ ParamNames.Id, user.Id },
			{ ParamNames.Provider, provider },
			{ ParamNames.Token, token },
			{ ParamNames.Expires, expires }
		};
		if (!String.IsNullOrEmpty(tokenToRemove))
			exp.Add("Remove", tokenToRemove);
		return _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].AddToken", exp);
	}

	public async Task<String?> GetTokenAsync(AppUser<T> user, String provider, String token)
	{
		var exp = new ExpandoObject()
		{
			{ ParamNames.Id, user.Id },
			{ ParamNames.Provider, provider },
			{ ParamNames.Token, token }
		};
		var res = await _dbContext.LoadAsync<JwtToken>(DataSource, $"[{DbSchema}].GetToken", exp);
		return res?.Token;
	}

	public Task RemoveTokenAsync(AppUser<T> user, String provider, String token)
	{
		var exp = new ExpandoObject()
		{
			{ ParamNames.Id, user.Id },
			{ ParamNames.Provider, provider },
			{ ParamNames.Token, token }
		};
		return _dbContext.ExecuteExpandoAsync(DataSource, $"[{DbSchema}].RemoveToken", exp);
	}
	#endregion
}

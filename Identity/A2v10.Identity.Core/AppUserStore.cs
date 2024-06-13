// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Web.Identity;

using System.Collections.Generic;
using System.Dynamic;
using System.Security.Claims;
using System.Threading;
using System.ComponentModel;

using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;

using A2v10.Data.Interfaces;
using A2v10.Identity.Core.Helpers;

using System.Diagnostics.CodeAnalysis;
using System.Linq;

public sealed class AppUserStore<T>(IDbContext dbContext, IOptions<AppUserStoreOptions<T>> options) :
	IUserStore<AppUser<T>>,
	IUserLoginStore<AppUser<T>>,
	IUserEmailStore<AppUser<T>>,
	IUserPhoneNumberStore<AppUser<T>>,
	IUserPasswordStore<AppUser<T>>,
	IUserSecurityStampStore<AppUser<T>>,
	IUserClaimStore<AppUser<T>>,
	IUserRoleStore<AppUser<T>>,
	IUserLockoutStore<AppUser<T>>,
	IUserTwoFactorStore<AppUser<T>>,
	IUserAuthenticatorKeyStore<AppUser<T>>
	//IUserTwoFactorTokenProvider<AppUser<T>>
	where T : struct
{
	private readonly IDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
	private readonly String? _dataSource = options.Value?.DataSource;
	private readonly String _dbSchema = options.Value?.Schema ?? "a2security";
	private readonly Boolean _multiTenant = options.Value?.MultiTenant ?? false;
	private readonly RolesMode _rolesMode = options.Value?.UseRoles ?? RolesMode.None;

	private readonly Func<AppUser<T>, IEnumerable<KeyValuePair<String, String?>>>? _addClaims = options.Value?.Claims;

	private static class ParamNames
	{
		public const String Id = nameof(Id);
        public const String Tenant = nameof(Tenant);
        public const String Provider = nameof(Provider);
		public const String Token = nameof(Token);
		public const String PasswordHash = nameof(PasswordHash);
		public const String SecurityStamp = nameof(SecurityStamp);
		public const String Confirmed = nameof(Confirmed);
		public const String Expires = nameof(Expires);
		public const String PhoneNumber = nameof(PhoneNumber);
		public const String PersonName = nameof(PersonName);
		public const String FirstName = nameof(FirstName);
		public const String LastName = nameof(LastName);
		public const String EmailConfirmed = nameof(EmailConfirmed);
		public const String PhoneNumberConfirmed = nameof(PhoneNumberConfirmed);
		public const String Email = nameof(Email);
		public const String Roles = nameof(Roles);
		public const String Branch = nameof(Branch);
        public const String ZipCode = nameof(ZipCode);
		public const String ExternalId = nameof(ExternalId);
		public const String AccessFailedCount = nameof(AccessFailedCount);
		public const String LockoutEndDate = nameof(LockoutEndDate);
        public const String Locale = nameof(Locale);
		public const String LoginProvider = nameof(LoginProvider);
        public const String ProviderKey = nameof(ProviderKey);
		public const String TwoFactorEnabled = nameof(TwoFactorEnabled);
		public const String AuthenticatorKey = nameof(AuthenticatorKey);

	}

    public async Task<IdentityResult> CreateAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		user.PasswordHash ??= user.PasswordHash2;
		user.SecurityStamp ??= user.SecurityStamp2;

		await _dbContext.ExecuteAsync<AppUser<T>>(_dataSource, $"[{_dbSchema}].[CreateUser]", user);
		return IdentityResult.Success;
	}

	public async Task<IdentityResult> DeleteAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		await _dbContext.ExecuteAsync<AppUser<T>>(_dataSource, $"[{_dbSchema}].[DeleteUser]", user);
		return IdentityResult.Success;
	}

	public void Dispose()
	{
		// do nothing?
	}

	[return: NotNull]
#if NET6_0
	public async Task<AppUser<T>> FindByIdAsync(String UserId, CancellationToken cancellationToken)
#elif NET7_0_OR_GREATER
	public async Task<AppUser<T>?> FindByIdAsync(String UserId, CancellationToken cancellationToken)
#endif
	{
		T? typedUserId = (T?)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(UserId);
		if (typedUserId == null)
			return new AppUser<T>();
		return await _dbContext.LoadAsync<AppUser<T>>(_dataSource, $"[{_dbSchema}].[FindUserById]", new { Id = typedUserId })
			?? new AppUser<T>();
	}

	[return: NotNull]
#if NET6_0
	public async Task<AppUser<T>> FindByNameAsync(String normalizedUserName, CancellationToken cancellationToken)
#elif NET7_0_OR_GREATER
	public async Task<AppUser<T>?> FindByNameAsync(String normalizedUserName, CancellationToken cancellationToken)
#endif
	{
		var UserName = normalizedUserName.ToLowerInvariant(); // A2v10
		var user = await _dbContext.LoadAsync<AppUser<T>>(_dataSource, $"[{_dbSchema}].[FindUserByName]", new { UserName });
		return user ?? new AppUser<T>();
	}

	public Task<String?> GetNormalizedUserNameAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String?>(user.UserName?.ToLowerInvariant() ?? String.Empty);
	}

	public Task<String> GetUserIdAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String>(user.Id.ToString()!);
	}

	public Task<String?> GetUserNameAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String?>(user.UserName);
	}

	public Task SetNormalizedUserNameAsync(AppUser<T> user, String? normalizedName, CancellationToken cancellationToken)
	{
		user.UserName = normalizedName?.ToLowerInvariant();
		return Task.CompletedTask;
	}

	public Task SetUserNameAsync(AppUser<T> user, String? userName, CancellationToken cancellationToken)
	{
		user.UserName = userName ?? throw new ArgumentNullException(nameof(userName));
		return Task.CompletedTask;
	}

	public async Task<IdentityResult> UpdateAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		if (user.Flags == 0)
			return IdentityResult.Success;

		var prm = new ExpandoObject()
		{
			{ ParamNames.Id,  user.Id }
		};
		if (user.Flags.HasFlag(UpdateFlags.PhoneNumber))
			prm.Add(ParamNames.PhoneNumber, user.PhoneNumber);
		if (user.Flags.HasFlag(UpdateFlags.Email))
			prm.Add(ParamNames.Email, user.Email);
		if (user.Flags.HasFlag(UpdateFlags.PersonName))
			prm.Add(ParamNames.PersonName, user.PersonName);
		if (user.Flags.HasFlag(UpdateFlags.FirstName))
			prm.Add(ParamNames.FirstName, user.FirstName);
		if (user.Flags.HasFlag(UpdateFlags.LastName))
			prm.Add(ParamNames.LastName, user.LastName);
		if (user.Flags.HasFlag(UpdateFlags.EmailConfirmed))
			prm.Add(ParamNames.EmailConfirmed, user.EmailConfirmed);
		if (user.Flags.HasFlag(UpdateFlags.PhoneNumberConfirmed))
			prm.Add(ParamNames.PhoneNumberConfirmed, user.PhoneNumberConfirmed);
		if (user.Flags.HasFlag(UpdateFlags.Roles))
			prm.Add(ParamNames.Roles, user.Roles);
		if (user.Flags.HasFlag(UpdateFlags.Branch))
			prm.Add(ParamNames.Branch, user.Branch);
        if (user.Flags.HasFlag(UpdateFlags.ZipCode))
            prm.Add(ParamNames.ZipCode, user.ZipCode);
		if (user.Flags.HasFlag(UpdateFlags.ExternalId))
			prm.Add(ParamNames.ExternalId, user.ExternalId);
		if (user.Flags.HasFlag(UpdateFlags.Locale))
            prm.Add(ParamNames.Locale, user.Locale);
		if (user.Flags.HasFlag(UpdateFlags.TwoFactor))
		{
			prm.Add(ParamNames.TwoFactorEnabled, user.TwoFactorEnabled);
			prm.Add(ParamNames.AuthenticatorKey, user.AuthenticatorKey);
		}

        await _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.UpdateParts]", prm);

		if (_multiTenant) // update segment!
			await _dbContext.ExecuteExpandoAsync(user.Segment, $"[{_dbSchema}].[User.UpdateParts]", prm);

		return IdentityResult.Success;
	}

	#region IUserSecurityStampStore
	public Task<String?> GetSecurityStampAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		// .net framework compatibility
		return Task.FromResult<String?>(user.SecurityStamp2 ?? user.SecurityStamp ?? String.Empty);
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
		await _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.SetSecurityStamp]", prm);
		user.SecurityStamp2 = stamp;
	}
	#endregion

	#region IUserEmailStore
	public Task SetEmailAsync(AppUser<T> user, String? email, CancellationToken cancellationToken)
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
		await _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.SetEMailConfirmed]", prm);
		if (_multiTenant)
			await _dbContext.ExecuteExpandoAsync(user.Segment, $"[{_dbSchema}].[User.SetEMailConfirmed]", prm);
		user.EmailConfirmed = confirmed;
	}

	[return: NotNull]
#if NET6_0
	public async Task<AppUser<T>> FindByEmailAsync(String normalizedEmail, CancellationToken cancellationToken)
#elif NET7_0_OR_GREATER
	public async Task<AppUser<T>?> FindByEmailAsync(String normalizedEmail, CancellationToken cancellationToken)
#endif
	{
		var Email = normalizedEmail?.ToLowerInvariant(); // A2v10
		return await _dbContext.LoadAsync<AppUser<T>>(_dataSource, $"[{_dbSchema}].[FindUserByEmail]", new { Email })
			?? new AppUser<T>();
	}

	public Task<String?> GetNormalizedEmailAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String?>(user.Email?.ToLowerInvariant() ?? String.Empty);
	}

	public Task SetNormalizedEmailAsync(AppUser<T> user, String? normalizedEmail, CancellationToken cancellationToken)
	{
		user.Email = normalizedEmail?.ToLowerInvariant();
		return Task.CompletedTask;
	}
#endregion

	#region IUserPasswordStore
	public async Task SetPasswordHashAsync(AppUser<T> user, String? passwordHash, CancellationToken cancellationToken)
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
		await _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.SetPasswordHash]", prm);
		user.PasswordHash2 = passwordHash;
	}

	public Task<String?> GetPasswordHashAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		// .net framework compatibility
		return Task.FromResult<String?>(user.PasswordHash2 ?? user.PasswordHash ?? String.Empty);
	}

	public Task<bool> HasPasswordAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
	#endregion

	#region IUserClaimStore
	public Task<IList<Claim>> GetClaimsAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		List<Claim> list = [];
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
		//????list.Add(new Claim(WellKnownClaims.NameIdentifier, user.Id.ToString()!));
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
		if (user.Branch != null)
			list.Add(new Claim(WellKnownClaims.Branch, user.Branch.ToString()!));
		if (user.OrganizationKey != null)
			list.Add(new Claim(WellKnownClaims.OrganizationKey, user.OrganizationKey));
		if (user.IsPersistent)
			list.Add(new Claim(WellKnownClaims.IsPersistent, "true"));
		if (!String.IsNullOrEmpty(user.Roles))
		{
			list.Add(new Claim(WellKnownClaims.Roles, user.Roles));
			if (user.Roles.Split(',').Any(x => x == WellKnownClaims.Admin))
				list.Add(new Claim(WellKnownClaims.Admin, WellKnownClaims.Admin));
		}

		//if (user.IsAdmin) // TODO ?? IsAdmin
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
		return (T?)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(value);
	}

	private async Task UpdateClaim(AppUser<T> user, Claim claim)
	{
		var prm = new ExpandoObject()
		{
			{ ParamNames.Id,  user.Id },
			{ claim.Type,  claim.Value}
		};
		await _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.Claim.{claim.Type}]", prm);

		switch (claim.Type)
		{
			case WellKnownClaims.Tenant:
				user.Tenant = ConvertTo(claim.Value);
				break;
			case WellKnownClaims.Organization:
				user.Organization = ConvertTo(claim.Value);
				break;
			case WellKnownClaims.Branch:
				user.Branch = ConvertTo(claim.Value);
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
            case WellKnownClaims.Roles:
                user.Roles = claim.Value;
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
	public async Task AddLoginAsync(AppUser<T> user, UserLoginInfo login, CancellationToken cancellationToken)
	{
        var prm = new ExpandoObject()
        {
            { ParamNames.Id,  user.Id },
			{ ParamNames.Tenant, user.Tenant },
            { ParamNames.LoginProvider, login.LoginProvider },
            { ParamNames.ProviderKey,  login.ProviderKey}
        };
        await _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.AddExternalLogin]", prm);
	}

	[return: NotNull]
#if NET6_0
#pragma warning disable CS8603 // Possible null reference return.
    public async Task<AppUser<T>> FindByLoginAsync(String loginProvider, String providerKey, CancellationToken cancellationToken)
#elif NET7_0_OR_GREATER
	public async Task<AppUser<T>?> FindByLoginAsync(String loginProvider, String providerKey, CancellationToken cancellationToken)
#endif
	{
		if (loginProvider == "ApiKey")
		{
			var prms = new ExpandoObject()
			{
				{ "ApiKey", providerKey }
			};
            return await _dbContext.LoadAsync<AppUser<T>>(_dataSource, $"[{_dbSchema}].[FindApiUserByApiKey]", prms);
        }
		else if (loginProvider == "PhoneNumber")
		{
			var prms = new ExpandoObject()
			{
				{ "PhoneNumber", providerKey }
			};
			return await _dbContext.LoadAsync<AppUser<T>>(_dataSource, $"[{_dbSchema}].[FindUserByPhoneNumber]", prms);

		}
		else if (loginProvider == "Email")
		{
			return await FindByEmailAsync(providerKey, cancellationToken);
		}
		else
		{
			var prms = new ExpandoObject()
				{
					{ ParamNames.LoginProvider, loginProvider },
					{ ParamNames.ProviderKey, providerKey },
				};
			return await _dbContext.LoadAsync<AppUser<T>>(_dataSource, $"[{_dbSchema}].[FindUserByExternalLogin]", prms);
		}
    }
#if NET6_0
#pragma warning restore CS8603 // Possible null reference return.
#endif
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
	public Task SetPhoneNumberAsync(AppUser<T> user, String? phoneNumber, CancellationToken cancellationToken)
	{
		user.PhoneNumber = phoneNumber;
		return Task.CompletedTask;
	}

	public Task<String?> GetPhoneNumberAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String?>(user.PhoneNumber);
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
			{ ParamNames.PhoneNumber, user.PhoneNumber
				?? throw new InvalidOperationException("PhoneNumber is null") },
			{ ParamNames.Confirmed,  confirmed}
		};
		await _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.SetPhoneNumberConfirmed]", prm);

		if (_multiTenant) // update segment!
			await _dbContext.ExecuteExpandoAsync(user.Segment, $"[{_dbSchema}].[User.SetPhoneNumberConfirmed]", prm);

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
		if (_multiTenant && user.Tenant != null)
			exp.Add(ParamNames.Tenant, user.Tenant);
		if (!String.IsNullOrEmpty(tokenToRemove))
			exp.Add("Remove", tokenToRemove);
		return _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].AddToken", exp);
	}

	public async Task<String?> GetTokenAsync(AppUser<T> user, String provider, String token)
	{
		var exp = new ExpandoObject()
		{
			{ ParamNames.Id, user.Id },
			{ ParamNames.Provider, provider },
			{ ParamNames.Token, token }
		};
		if (_multiTenant && user.Tenant != null)
			exp.Add(ParamNames.Tenant, user.Tenant);
		var res = await _dbContext.LoadAsync<JwtToken<T>>(_dataSource, $"[{_dbSchema}].GetToken", exp);
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
		if (_multiTenant && user.Tenant != null)
			exp.Add(ParamNames.Tenant, user.Tenant);
		return _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].RemoveToken", exp);
	}
	#endregion

	#region IUserRoleStore
	public Task AddToRoleAsync(AppUser<T> user, String roleName, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task RemoveFromRoleAsync(AppUser<T> user, String roleName, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task<IList<String>> GetRolesAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		List<String> list = [];
		if (_rolesMode == RolesMode.Claims && user.Roles != null)
			list.AddRange(user.Roles.Split(",")); 
		else if (_rolesMode == RolesMode.Database)
			throw new NotImplementedException();
		return Task.FromResult(list as IList<String>);	
	}

	public Task<Boolean> IsInRoleAsync(AppUser<T> user, string roleName, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}


	public Task<IList<AppUser<T>>> GetUsersInRoleAsync(String roleName, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
	#endregion

	#region IUserLockoutStore
	public Task<DateTimeOffset?> GetLockoutEndDateAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<DateTimeOffset?>(user.LockoutEndDateUtc);
	}

	public async Task SetLockoutEndDateAsync(AppUser<T> user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
	{
		user.LockoutEndDateUtc = lockoutEnd;	
		var prm = new ExpandoObject()
		{
			{ ParamNames.Id,  user.Id },
			{ ParamNames.LockoutEndDate,  user.LockoutEndDateUtc }
		};
		await _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.SetLockoutEndDate]", prm);
	}

	public async Task<Int32> IncrementAccessFailedCountAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		user.AccessFailedCount++;
		var prm = new ExpandoObject()
		{
			{ ParamNames.Id,  user.Id },
			{ ParamNames.AccessFailedCount,  user.AccessFailedCount }
		};
		await _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.SetAccessFailedCount]", prm);
		return user.AccessFailedCount;
	}

	public async Task ResetAccessFailedCountAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		if (user.AccessFailedCount == 0)
			return;
		user.AccessFailedCount = 0;
		var prm = new ExpandoObject()
		{
			{ ParamNames.Id,  user.Id },
			{ ParamNames.AccessFailedCount,  user.AccessFailedCount }
		};
		await _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.SetAccessFailedCount]", prm);
	}

	public Task<Int32> GetAccessFailedCountAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult(user.AccessFailedCount);
	}

	public Task<Boolean> GetLockoutEnabledAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult(user.LockoutEnabled);
	}

	public Task SetLockoutEnabledAsync(AppUser<T> user, bool enabled, CancellationToken cancellationToken)
	{
		user.LockoutEnabled = enabled;
		return Task.CompletedTask;
	}

	#endregion

	#region IUserTwoFactorStore
	public Task SetTwoFactorEnabledAsync(AppUser<T> user, Boolean enabled, CancellationToken cancellationToken)
	{
		user.TwoFactorEnabled = enabled;
		var exp = new ExpandoObject()
		{
			{ ParamNames.Id, user.Id },
			{ ParamNames.TwoFactorEnabled, enabled }
		};
		return _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.SetTwoFactorEnabled]", exp);
	}

	public Task<Boolean> GetTwoFactorEnabledAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<Boolean>(user.TwoFactorEnabled);
	}
	#endregion

	#region IUserAuthenticatorKeyStore
	public Task SetAuthenticatorKeyAsync(AppUser<T> user, String key, CancellationToken cancellationToken)
	{
		user.AuthenticatorKey = key;
		var exp = new ExpandoObject()
		{
			{ ParamNames.Id, user.Id },
			{ ParamNames.AuthenticatorKey, key }
		};
		return _dbContext.ExecuteExpandoAsync(_dataSource, $"[{_dbSchema}].[User.SetAuthenticatorKey]", exp);
	}

	public Task<String?> GetAuthenticatorKeyAsync(AppUser<T> user, CancellationToken cancellationToken)
	{
		return Task.FromResult<String?>(user.AuthenticatorKey);
	}
	#endregion
}

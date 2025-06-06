﻿// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading;
using System.Dynamic;
using System.Linq;
using System.Collections.Generic;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

using A2v10.Web.Identity;

namespace A2v10.Platform.Web;
public record UserIdentity : IUserIdentity
{
	public Int64? Id { get; init; }
	public String? Name { get; init; }
	public String? PersonName { get; init; }
	public String? FirstName { get; init; }
	public String? LastName { get; init; }
	public Int32? Tenant { get; set; }
	public String? Segment { get; init; }

	public Boolean IsAdmin { get; init; }
	public Boolean IsTenantAdmin { get; init; }
	public IEnumerable<String>? Roles { get; init; }

	public void SetInitialTenantId(Int32 tenant)
	{
		Tenant = tenant;
	}
}

public record UserState : IUserState
{
	public Int64? Company { get; set; }
	public Boolean IsReadOnly { get; set; }
	public Boolean IsAdmin { get; set; }
	public String? Permissions { get; set; }

	// TODO: isValud???
	public Boolean Invalid { get; set; }
	public String? Message { get; init; }
	public List<Guid> Modules { get; init; } = [];
	IEnumerable<Guid> IUserState.Modules => Modules;
}

public record UserLocale : IUserLocale
{
	public UserLocale(String? locale = null)
    {
		Locale = locale ??
			Thread.CurrentThread.CurrentUICulture.Name;
    }
	public String Locale { get; }
	public String Language => Locale[..2];
}

public class CurrentUser : ICurrentUser, IDbIdentity
{
	public IUserIdentity Identity { get; private set; } = new UserIdentity();
	public UserState State { get; private set; } = new();
	public IUserLocale Locale { get; private set; } = new UserLocale();

	#region IDbIdentity
	public Int32? TenantId => Identity?.Tenant;
	public Int64? UserId => Identity?.Id;
	public String? Segment => Identity?.Segment;
	#endregion

	IUserState ICurrentUser.State => State;

	public String CookiePrefix { get; } = String.Empty;

	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IDataProtector _protector;
	private readonly IPermissionBag _permissionBag;

	public CurrentUser(IHttpContextAccessor httpContextAccessor, IDataProtectionProvider dataProtectionProvider,
		IOptions<AppOptions> options, IPermissionBag permissionBag)
	{
		_httpContextAccessor = httpContextAccessor;
		_protector = dataProtectionProvider.CreateProtector("State");
		_permissionBag = permissionBag;
		var px = options?.Value?.CookiePrefix;
		if (!String.IsNullOrEmpty(px))
            CookiePrefix = px + '.';
    }

	public void Setup(HttpContext context)
	{
		SetupUserIdentity(context);
		SetupUserState(context);
		SetupUserLocale(context);
	}

	void SetupUserIdentity(HttpContext context) 
	{ 
		var ident = context.User.Identity;
		if (ident != null && ident.IsAuthenticated) {
			Identity = new UserIdentity()
			{
				Id = ident.GetUserId<Int64>(),
				Tenant = ident.GetUserTenant<Int32>(),
				Name = ident.Name,
				PersonName = ident.GetUserPersonName(),
				FirstName = ident.GetUserFirstName(),
				LastName = ident.GetUserLastName(),
				Segment = ident.GetUserSegment(),
				IsAdmin = ident.IsUserAdmin(),
				IsTenantAdmin = ident.IsTenantAdmin(),
				Roles = ident.GetUserRolesList().ToList()
			};
		}
	}

	private String CookieName => $"{CookiePrefix}{CookieNames.Identity.State}";

	public Boolean IsPermissionEnabled(String key, PermissionFlag flag)
	{
		if (State.IsAdmin)
			return true;
		var permissionFlags = _permissionBag.DecodePermissions(State.Permissions);
		if (permissionFlags.TryGetValue(key, out var flags))
			return flags.HasFlag(flag);
		return false;
	}

	void SetupUserState(HttpContext context)
	{
		var state = context.Request.Cookies[CookieName];
		if (!String.IsNullOrEmpty(state))
		{
			try
			{
				State = JsonConvert.DeserializeObject<UserState>(_protector.Unprotect(state))!;
			} 
			catch (Exception ex)
			{
				State = new UserState()
				{
					Invalid = true,
					Message = ex.Message
				};
			}
		}
	}

	void SetupUserLocale(HttpContext context)
	{
		var ident = context.User.Identity;
		if (ident == null || !ident.IsAuthenticated)
			return;
		var userLoc = ident.GetUserLocale();
		if (context.Request.Query.ContainsKey("lang"))
		{
			//var lang = context.Request.Query["lang"];
			// TODO: check available locales
		}
		Locale = new UserLocale(userLoc);
	}

	public void SetCompanyId(Int64 id)
	{
		if (State == null)
			throw new InvalidProgramException("There is no current user state");
		State.Company = id;
		StoreState();
	}

    public void AddModules(IEnumerable<Guid> modules)
	{
		foreach (var module in modules)
			State.Modules.Add(module);
        StoreState();
    }

	public void SetInitialTenantId(Int32 tenantId)
	{
		Identity.SetInitialTenantId(tenantId);
	}
	public void SetUserState(Boolean admin, Boolean readOnly, String? permissions)
	{
		if (State == null)
			throw new InvalidProgramException("There is no current user state");
		State.IsReadOnly = readOnly;
		State.Permissions = permissions;
		State.IsAdmin = admin;
		StoreState();
	}

	public ExpandoObject DefaultParams()
	{
		var prms = new ExpandoObject();
		if (Identity.Id != null)
			prms.Add("UserId", Identity.Id);
		if (Identity.Tenant != null && Identity.Tenant != 0)
			prms.Add("TenantId", Identity.Tenant);
		return prms;
	}

	void StoreState()
	{
		var stateJson = JsonConvert.SerializeObject(State, new JsonSerializerSettings()
		{
			NullValueHandling = NullValueHandling.Ignore,
			DefaultValueHandling = DefaultValueHandling.Ignore,
		});
		_httpContextAccessor?.HttpContext?.Response.Cookies.Append(CookieName, _protector.Protect(stateJson), 
			new CookieOptions()
			{
				SameSite = SameSiteMode.Strict,
				Secure = true,
				HttpOnly = true,
			});
	}
}



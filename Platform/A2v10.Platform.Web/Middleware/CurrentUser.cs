﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using Microsoft.AspNetCore.Http;

using Microsoft.AspNetCore.DataProtection;

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

    public Int32? Tenant { get; init; }
    public String? Segment { get; init; }

    public Boolean IsAdmin { get; init; }
    public Boolean IsTenantAdmin { get; init; }
}

public record UserState : IUserState
{
    public Int64? Company { get; set; }
    public Boolean IsReadOnly { get; set; }

    // TODO: isValud???
    public Boolean Invalid { get; set; }
    public String? Message { get; init; }
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
    public UserState State { get; private set; }
    public IUserLocale Locale { get; private set; } = new UserLocale();

    #region IDbIdentity
    public Int32? TenantId => Identity?.Tenant;
    public Int64? UserId => Identity?.Id;
    public String? Segment => Identity?.Segment;
    #endregion

    IUserState ICurrentUser.State => State;

    public Boolean IsAdminApplication { get; private set; }

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtector _protector;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, IDataProtectionProvider dataProtectionProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _protector = dataProtectionProvider.CreateProtector("State");
        State = new UserState()
        {
            Invalid = true
        };
    }

    public void Setup(HttpContext context)
    {
        SetupUserIdentity(context);
        SetupUserState(context);
        SetupUserLocale(context);
        IsAdminApplication = context.Request.Path.StartsWithSegments("/admin");
    }

    void SetupUserIdentity(HttpContext context)
    {
        var ident = context.User.Identity;
        if (ident != null && ident.IsAuthenticated)
        {
            Identity = new UserIdentity()
            {
                Id = ident.GetUserId<Int64>(),
                Tenant = ident.GetUserTenantId(),
                Name = ident.Name,
                PersonName = ident.GetUserPersonName(),
                Segment = ident.GetUserSegment(),
                IsAdmin = ident.IsUserAdmin(),
                IsTenantAdmin = ident.IsTenantAdmin()
            };
        }
    }

    void SetupUserState(HttpContext context)
    {
        var state = context.Request.Cookies[CookieNames.Identity.State];
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
        var userLoc = ident.GetUserLocale();
        if (context.Request.Query.ContainsKey("lang"))
        {
            var lang = context.Request.Query["lang"];
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

    public void SetReadOnly(Boolean readOnly)
    {
        if (State == null)
            throw new InvalidProgramException("There is no current user state");
        State.IsReadOnly = readOnly;
        StoreState();
    }

    void StoreState()
    {
        var stateJson = JsonConvert.SerializeObject(State);
        _httpContextAccessor?.HttpContext?.Response.Cookies.Append(CookieNames.Identity.State, _protector.Protect(stateJson),
            new CookieOptions()
            {
                SameSite = SameSiteMode.Strict,
                Secure = true,
                HttpOnly = true,
            });
    }
}


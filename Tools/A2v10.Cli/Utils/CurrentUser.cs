using System;
using System.Collections.Generic;
using System.Dynamic;

using A2v10.Infrastructure;

namespace A2v10.Cli;

internal record UserIdentity : IUserIdentity
{
    public long? Id => 99; // default admin
    public string? Name => null;

    public string? PersonName => null;

    public int? Tenant => null;

    public string? Segment => null;

    public bool IsAdmin => false;

    public bool IsTenantAdmin => false;

    public string? Theme => null;

    public IEnumerable<string>? Roles => [];

    public void SetInitialTenantId(int tenant)
    {
    }
}

public record UserState: IUserState
{
    public long? Company => null;
    public bool IsReadOnly => false;
    public IEnumerable<Guid> Modules => [];
}

public record UserLocale : IUserLocale
{
    public string Locale => "en-US";
    public string Language => "en";
}

internal record CurrentUser : ICurrentUser
{
    public IUserIdentity Identity => new UserIdentity();

    public IUserState State => new UserState();

    public IUserLocale Locale => new UserLocale();

    public void AddModules(IEnumerable<Guid> modules)
    {
    }

    public ExpandoObject DefaultParams()
    {
        return new ExpandoObject()
        {
            {"UserId", Identity.Id }
        };
    }

    public bool IsPermissionEnabled(string key, PermissionFlag flag)
    {
        return true;
    }

    public void SetCompanyId(long id)
    {
    }

    public void SetInitialTenantId(int tenantId)
    {
    }

    public void SetUserState(bool admin, bool readOnly, string? permissions)
    {
    }
}

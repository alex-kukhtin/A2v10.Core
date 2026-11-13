using System;
using System.Collections.Generic;
using System.Dynamic;

using Microsoft.Extensions.Configuration;

using A2v10.Infrastructure;
using A2v10.Platform.Web;

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

// in the application's locale: validate looks keys up in the dictionary a page reads
internal record CurrentUser(IConfiguration Configuration) : ICurrentUser
{
    public IUserIdentity Identity => new UserIdentity();

    public IUserState State => new UserState();

    public IUserLocale Locale => new UserLocale(AppLocale.Of(Configuration));

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

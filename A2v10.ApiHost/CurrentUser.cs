using System.Dynamic;
using A2v10.Infrastructure;

namespace A2v10.ApiHost;

public class UserIdentity : IUserIdentity
{
    public Int64? Id => 99;

    public String? Name => throw new NotImplementedException();

    public String? PersonName => throw new NotImplementedException();

    public int? Tenant => null;

    public String? Segment => null;

    public Boolean IsAdmin => throw new NotImplementedException();

    public bool IsTenantAdmin => throw new NotImplementedException();

    public string? Theme => throw new NotImplementedException();

    public IEnumerable<String>? Roles => [];

    public void SetInitialTenantId(int tenant)
    {
        throw new NotImplementedException();
    }
}

public class UserState : IUserState
{
    public long? Company => null;

    public bool IsReadOnly => false;

    public IEnumerable<Guid> Modules => [];
}
public class CurrentUser : ICurrentUser
{
    public CurrentUser(IHttpContextAccessor _contextAccessor)
    {

    }
    private UserIdentity _identity = new();
    private UserState _state = new();
    public IUserIdentity Identity => _identity;

    public IUserState State => _state;

    public IUserLocale Locale => throw new NotImplementedException();

    public void AddModules(IEnumerable<Guid> modules)
    {
    }

    public ExpandoObject DefaultParams()
    {
        throw new NotImplementedException();
    }

    public bool IsPermissionEnabled(string key, PermissionFlag flag)
    {
        throw new NotImplementedException();
    }

    public void SetCompanyId(long id)
    {
        throw new NotImplementedException();
    }

    public void SetInitialTenantId(int tenantId)
    {
        throw new NotImplementedException();
    }

    public void SetUserState(bool admin, bool readOnly, string? permissions)
    {
        throw new NotImplementedException();
    }
}

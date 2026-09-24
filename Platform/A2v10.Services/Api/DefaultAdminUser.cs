// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Threading;

namespace A2v10.Services.Api;

/* The user of a caller without a request: 99, the one user a2v10_platform_simple.sql inserts into
 * an empty database, an admin. Not registered by UseEndpointDataServices - who the user is, the
 * host decides: the scenario tests register this one, the API will authenticate its own.
 */
public sealed class DefaultAdminUser : ICurrentUser
{
    public IUserIdentity Identity { get; } = new AdminIdentity();
    public IUserState State { get; } = new AdminState();
    public IUserLocale Locale { get; } = new ThreadLocale(Thread.CurrentThread.CurrentUICulture.Name);

    public Boolean IsPermissionEnabled(String key, PermissionFlag flag) => true;

    public ExpandoObject DefaultParams() => new() { { "UserId", Identity.Id } };

    public void SetCompanyId(Int64 id) { }
    public void SetInitialTenantId(Int32 tenantId) { }
    public void SetUserState(Boolean admin, Boolean readOnly, String? permissions) { }
    public void AddModules(IEnumerable<Guid> modules) { }

    private sealed record AdminIdentity : IUserIdentity
    {
        public Int64? Id => 99;
        public String? Name => null;
        public String? PersonName => null;
        public Int32? Tenant => null;
        public String? Segment => null;
        public Boolean IsAdmin => true;
        public Boolean IsTenantAdmin => false;
        public String? Theme => null;
        public IEnumerable<String>? Roles => [];
        public void SetInitialTenantId(Int32 tenant) { }
    }

    private sealed record AdminState : IUserState
    {
        public Int64? Company => null;
        public Boolean IsReadOnly => false;
        public IEnumerable<Guid> Modules => [];
    }

    // as the web user's without a locale claim: the culture of the thread
    private sealed record ThreadLocale(String Locale) : IUserLocale
    {
        public String Language => Locale[..2];
    }
}

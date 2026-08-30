// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Infrastructure;

namespace A2v10.Metadata.Tests;

/* The two ambient services every load path asks for and no test has an opinion about. Same
 * shape as the CLI's (Tools/A2v10.Cli/Utils): a fixed admin id and a localizer that returns
 * what it is given, so a generated string is compared to what the generator wrote.
 */
internal record TestUserIdentity : IUserIdentity
{
    public Int64? Id => 99;
    public String? Name => null;
    public String? PersonName => null;
    public Int32? Tenant => null;
    public String? Segment => null;
    public Boolean IsAdmin => false;
    public Boolean IsTenantAdmin => false;
    public String? Theme => null;
    public IEnumerable<String>? Roles => [];
    public void SetInitialTenantId(Int32 tenant) { }
}

internal record TestUserState : IUserState
{
    public Int64? Company => null;
    public Boolean IsReadOnly => false;
    public IEnumerable<Guid> Modules => [];
}

internal record TestUserLocale : IUserLocale
{
    public String Locale => "en-US";
    public String Language => "en";
}

internal record TestCurrentUser : ICurrentUser
{
    public IUserIdentity Identity => new TestUserIdentity();
    public IUserState State => new TestUserState();
    public IUserLocale Locale => new TestUserLocale();

    public void AddModules(IEnumerable<Guid> modules) { }

    public ExpandoObject DefaultParams() =>
        new() { { "UserId", Identity.Id } };

    public Boolean IsPermissionEnabled(String key, PermissionFlag flag) => true;

    public void SetCompanyId(Int64 id) { }
    public void SetInitialTenantId(Int32 tenantId) { }
    public void SetUserState(Boolean admin, Boolean readOnly, String? permissions) { }
}

internal class TestLocalizer : ILocalizer
{
    public String? this[String? index] => index;
    public IDictionary<String, String> Dictionary => new Dictionary<String, String>();
    public String? Localize(String? locale, String? content, Boolean replaceNewLine = true) => content;
    public String? Localize(String? content) => content;
}

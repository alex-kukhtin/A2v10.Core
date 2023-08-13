// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services.Javascript;

public class ScriptUser
{
#pragma warning disable IDE1006 // Naming Styles
    public Int32 tenantId { get; }
    public Int64 userId { get; }
    public String segment { get; }
#pragma warning restore IDE1006 // Naming Styles
    public ScriptUser(ICurrentUser currentUser)
    {
        tenantId = currentUser.Identity.Tenant ?? 1;
        userId = currentUser.Identity.Id ?? 0;
        segment = currentUser.Identity.Segment ?? String.Empty;
    }
}

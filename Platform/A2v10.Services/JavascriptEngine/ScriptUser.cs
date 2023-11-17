// Copyright © 2020-2023 Oleksandr Kukhtin. All rights reserved.

namespace A2v10.Services.Javascript;

public class ScriptUser(ICurrentUser currentUser)
{
#pragma warning disable IDE1006 // Naming Styles
    public Int32 tenantId { get; } = currentUser.Identity.Tenant ?? 1;
    public Int64 userId { get; } = currentUser.Identity.Id ?? 0;
    public String segment { get; } = currentUser.Identity.Segment ?? String.Empty;
}

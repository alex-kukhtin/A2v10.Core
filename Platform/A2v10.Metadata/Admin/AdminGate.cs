// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The one right of the 'admin' namespace. Read from UserRoles and not from the claim, as the menu's
 * [User.Grants.Load] does: the claim is rebuilt with the stamp, up to five minutes late, and a taken
 * role must close these screens at once - they are where roles are given.
 */
internal static class AdminGate
{
    internal static async Task CheckAsync(IServiceProvider serviceProvider, String? dataSource)
    {
        var dbContext = serviceProvider.GetRequiredService<IDbContext>();
        var currentUser = serviceProvider.GetRequiredService<ICurrentUser>();
        var sql = $"""
        set nocount on;
        set transaction isolation level read committed;

        select [State!TState!Object] = null, IsAdmin = cast(case when exists(
          select 1 from a2security.UserRoles where UserId = @UserId and [Role] = N'{AppRoles.Admin}'
        ) then 1 else 0 end as bit);
        """;
        var dm = await dbContext.LoadModelSqlAsync(dataSource, sql,
            dbprms => dbprms.AddBigInt("@UserId", currentUser.Identity.Id));
        if (!dm.Eval<Boolean>("State.IsAdmin"))
            throw new InvalidOperationException("UI:@[UIError.AccessDenied]");
    }
}

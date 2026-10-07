// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal class SqlBuilderUsers(IServiceProvider serviceProvider, AppPlatformId _platformId)
{
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();

    // the card's recordset of the boundary, and the save's parameter of the same rows
    internal const String BoundaryRows = "Boundary";

    // the people who sign in: ViewUsers drops the deleted and the system row 0, the join drops the API keys
    private const String UserFrom = """
        a2security.ViewUsers v inner join a2security.Users u on u.Id = v.Id and isnull(u.IsApiUser, 0) = 0
        """;

    // the role as a person reads it
    private const String RoleName = "isnull(r.[Name], r.[Id])";

    /* One list for the row and the card: the card's save is merged into its row, so a column missing
     * here leaves it stale. Roles are the names, for a person to read - ViewUsers.Roles is the ids,
     * comma-joined for the claim, and stays so.
     */
    private const String UserColumns = $"""
        [Id!!Id] = v.Id, v.UserName, v.PersonName, v.Email, v.PhoneNumber, v.Memo,
          [LastLoginDate!!Utc] = v.LastLoginDate, v.EmailConfirmed,
          IsBlocked = isnull(v.IsBlocked, 0),
          Roles = (select string_agg({RoleName}, N', ') within group (order by {RoleName})
            from a2security.UserRoles ur inner join a2security.Roles r on r.[Id] = ur.[Role]
            where ur.UserId = v.Id and r.[Void] = 0)
        """;

    /* What the card offers. Everyone is held by all without a row; a void role is hidden, and its
     * assignment kept - the role may come back, and the assignment with it.
     */
    private const String Assignable = $"r.[Void] = 0 and r.[Id] <> N'{AppRoles.Everyone}'";

    // an empty field of the form: the client takes the length from the column's type, and N'' is nvarchar(1)
    private const String Empty = "cast(null as nvarchar(255))";

    private const String Head = """
        set nocount on;
        set transaction isolation level read committed;
        """;

    private static String CardSql(IReadOnlyList<BoundaryDimension> boundary) => $"""

        select [User!TUser!Object] = null, {UserColumns}
        from {UserFrom}
        where v.Id = @Id;

        select [Roles!TRole!Array] = null, [Id!!Id] = r.[Id], [Name] = {RoleName}, r.[Memo],
          [Checked] = cast(case when exists(
            select 1 from a2security.UserRoles ur where ur.UserId = @Id and ur.[Role] = r.[Id]
          ) then 1 else 0 end as bit)
        from a2security.Roles r
        where {Assignable}
        order by case when r.[Id] = N'{AppRoles.Admin}' then 0 else 1 end, {RoleName};
        {BoundarySql(boundary)}
        """;

    /* Every dimension in one recordset, told apart by Boundary - the dimension's Path. A value the
     * picker would not offer is left out, unless it is assigned: a void store still in the user's
     * boundary shows, so it can be unticked. No [!!Id]: ids of two catalogs may meet in one array.
     */
    private static String BoundarySql(IReadOnlyList<BoundaryDimension> boundary)
    {
        if (boundary.Count == 0)
            return String.Empty;
        var selects = boundary.Select(d => $"""
            select [Boundary] = N'{d.Path}', [Id] = c.[Id], [Name] = c.[Name], [Void] = c.[Void],
              [Checked] = cast(case when b.UserId is null then 0 else 1 end as bit)
            from {d.Catalog.SqlTableName} c
              left join {d.SqlTableName} b on b.UserId = @Id and b.[{d.Column}] = c.[Id]
            where c.[Void] = 0 or b.UserId is not null
            """);
        return $"""

            select [{BoundaryRows}!TBoundary!Array] = null, [Boundary], [Id], [Name], [Void], [Checked]
            from (
            {String.Join($"{Environment.NewLine}union all{Environment.NewLine}", selects)}
            ) x
            order by [Boundary], [Name];
            """;
    }

    // a merge per dimension, over its own rows of the one parameter
    private static String BoundaryMergeSql(IReadOnlyList<BoundaryDimension> boundary) =>
        String.Join(Environment.NewLine, boundary.Select(d => $"""

            merge {d.SqlTableName} as t
            using (select [Id], [Checked] from @{BoundaryRows} where [Boundary] = N'{d.Path}') as s
            on t.UserId = @Id and t.[{d.Column}] = s.[Id]
            when matched and s.[Checked] = 0 then delete
            when not matched by target and s.[Checked] = 1 then insert (UserId, [{d.Column}]) values (@Id, s.[Id]);
            """));

    public Task<IDataModel> LoadIndexAsync(String? dataSource)
    {
        var sql = $"""
        {Head}
        select [Users!TUser!Array] = null, {UserColumns}
        from {UserFrom}
        order by v.UserName;
        """;
        return _dbContext.LoadModelSqlAsync(dataSource, sql, _ => { });
    }

    public Task<IDataModel> LoadCardAsync(String? dataSource, Int64 id, IReadOnlyList<BoundaryDimension> boundary)
    {
        return _dbContext.LoadModelSqlAsync(dataSource, Head + CardSql(boundary), dbprms => dbprms.AddBigInt("@Id", id));
    }

    // the form of the new user: nothing to read, but a dialog takes its model from the server
    public Task<IDataModel> LoadNewAsync(String? dataSource)
    {
        var sql = $"""
        {Head}
        select [User!TUser!Object] = null, [Id!!Id] = cast(0 as bigint),
          UserName = {Empty}, PersonName = {Empty}, [Password] = {Empty}, [Confirm] = {Empty};
        """;
        return _dbContext.LoadModelSqlAsync(dataSource, sql, _ => { });
    }

    public Task<IDataModel> LoadPasswordAsync(String? dataSource, Int64 id)
    {
        var sql = $"""
        {Head}
        select [User!TUser!Object] = null, [Id!!Id] = v.Id, v.UserName, [Password] = {Empty}, [Confirm] = {Empty}
        from a2security.ViewUsers v
        where v.Id = @Id;
        """;
        return _dbContext.LoadModelSqlAsync(dataSource, sql, dbprms => dbprms.AddBigInt("@Id", id));
    }

    /* The profile, the roles and the boundary in one transaction. Roles and boundary come back as the
     * card shows them, a row per offered value with its Checked; a value the card did not offer is not
     * in them, so it stays as is.
     */
    public async Task<ExpandoObject> SaveCardAsync(String? dataSource, Int64 id, ExpandoObject user,
        IEnumerable<ExpandoObject> roles, IEnumerable<ExpandoObject> boundaryRows, IReadOnlyList<BoundaryDimension> boundary)
    {
        var sql = $"""
        {Head}
        set xact_abort on;

        begin tran;

        update a2security.Users set PersonName = @PersonName, PhoneNumber = @PhoneNumber, Memo = @Memo
        where Id = @Id and Void = 0;

        merge a2security.UserRoles as t
        using (
          select s.[Id], s.[Checked] from @Roles s inner join a2security.Roles r on r.[Id] = s.[Id]
          where {Assignable}
        ) as s
        on t.UserId = @Id and t.[Role] = s.[Id]
        when matched and s.[Checked] = 0 then delete
        when not matched by target and s.[Checked] = 1 then insert (UserId, [Role]) values (@Id, s.[Id]);
        {BoundaryMergeSql(boundary)}

        commit tran;
        """ + CardSql(boundary);

        var rolesTable = new DataTable();
        rolesTable.Columns.Add("Id", typeof(String));
        rolesTable.Columns.Add("Checked", typeof(Boolean));
        foreach (var role in roles)
            rolesTable.Rows.Add(role.Get<String>("Id"), role.Get<Boolean>("Checked"));

        // the column order is the type's (CliDatabaseCreator.CreateUserBoundaryTableType)
        var boundaryTable = new DataTable();
        boundaryTable.Columns.Add("Boundary", typeof(String));
        boundaryTable.Columns.Add("Id", _platformId.ClrType);
        boundaryTable.Columns.Add("Checked", typeof(Boolean));
        foreach (var row in boundaryRows)
            boundaryTable.Rows.Add(row.Get<String>("Boundary"), row.Get<Object>("Id"), row.Get<Boolean>("Checked"));

        var dm = await _dbContext.LoadModelSqlAsync(dataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@Id", id);
            dbprms.AddString("@PersonName", user.Get<String>("PersonName"));
            dbprms.AddString("@PhoneNumber", user.Get<String>("PhoneNumber"));
            dbprms.AddString("@Memo", user.Get<String>("Memo"));
            dbprms.AddStructured("@Roles", Constants.SqlNames.UserRoleTableType, rolesTable);
            if (boundary.Count > 0)
                dbprms.AddStructured($"@{BoundaryRows}", Constants.SqlNames.UserBoundaryTableType, boundaryTable);
        }) ?? throw new InvalidOperationException("Users. Save failed. DataModel is null");
        return dm.Root;
    }

    // after IAppUserAdmin.CreateAsync: the profile is the screen's to write, and the row goes back into the list
    public Task<IDataModel> SetPersonNameAsync(String? dataSource, Int64 id, String? personName)
    {
        var sql = $"""
        {Head}
        update a2security.Users set PersonName = @PersonName where Id = @Id;

        select [User!TUser!Object] = null, {UserColumns}
        from {UserFrom}
        where v.Id = @Id;
        """;
        return _dbContext.LoadModelSqlAsync(dataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@Id", id);
            dbprms.AddString("@PersonName", personName);
        });
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The logins one may pick now, read-only, for everyone who reads a record with a 'user' column - no
 * gate of its own. The resolve reads every row (UsersRefSourceName); here the deleted, the system row 0,
 * the blocked and the API keys drop out, as a void value drops out of a set's candidates. The name is
 * the view's, so it is composed in one place. Whole, like the operations: logins come by the dozen.
 */
internal class SqlBuilderUserPicker(IServiceProvider serviceProvider)
{
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();

    private static readonly TableMetadata _table = TableMetadataDefaults.UsersTable();

    private static readonly String _candidates = $"""
        {TableMetadataDefaults.UsersRefSourceName} a inner join a2security.Users u on u.Id = a.Id
        where u.Void = 0 and u.Id <> 0 and isnull(u.IsBlocked, 0) = 0 and isnull(u.IsApiUser, 0) = 0
        """;

    public Task<IDataModel> LoadBrowseModel(String? dataSource)
    {
        var sql = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        select [{_table.CollectionName}!{_table.TypeName}!Array] = null,
          [Id!!Id] = a.[Id], [Name!!Name] = a.[Name], u.UserName
        from {_candidates}
        order by a.[Name];
        """;
        return _dbContext.LoadModelSqlAsync(dataSource, sql, _ => { });
    }

    // type-ahead in a selector pointing here, the shape SqlBuilder.FetchAsync returns
    public async Task<IInvokeResult> FetchAsync(String? dataSource, ExpandoObject? prms)
    {
        var sql = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        declare @fr nvarchar(255) = N'%' + @Text + N'%';

        select top(100) [{_table.CollectionName}!{_table.TypeName}!Array] = null,
          [Id!!Id] = a.[Id], [Name!!Name] = a.[Name]
        from {_candidates} and (a.[Name] like @fr or u.UserName like @fr)
        order by a.[Name];
        """;
        var model = await _dbContext.LoadModelSqlAsync(dataSource, sql,
            dbprms => dbprms.AddString("@Text", prms?.Get<String>("Text")));
        return model.ToInvokeResult();
    }
}

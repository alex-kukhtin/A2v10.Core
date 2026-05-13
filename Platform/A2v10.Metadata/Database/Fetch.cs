// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    internal Task<IInvokeResult> FetchFolderAsync(ExpandoObject? prms)
    {
        throw new InvalidOperationException("IMPLEMENT FETCH FOLDER");
    }

    internal async Task<IInvokeResult> FetchAsync(ExpandoObject? prms)
    {
        var sql = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        declare @fr nvarchar(255);
        set @fr = N'%' + @Text + N'%';

        select top(100) [{_table.CollectionName}!{_table.TypeName}!Array] = null, 
            [Id!!Id] = a.Id, [Name!!Name] = a.[Name]
        from {_table.SqlTableName} a
        where a.[Void] = 0 and
            (a.[Name] like @fr)
        order by a.[Name];
        """;

        var model = await _dbContext.LoadModelSqlAsync(_dataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Text", prms?.Get<String>("Text"));
        });

        return model.ToInvokeResult();
    }
}

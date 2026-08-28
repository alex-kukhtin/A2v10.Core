// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The operation registry, read-only. Small enough that it is fetched whole: no paging, no
 * fragment, no filters - which is the reason this endpoint left the generated pipeline, where
 * every index carries all three whether it needs them or not.
 *
 * The envelope names come off OperationsTable, because the selector that opens this dialog reads
 * the collection by the same name the reference resolved to.
 */
internal class SqlBuilderOperations(IServiceProvider serviceProvider)
{
    private readonly IDbContext _dbContext = serviceProvider.GetRequiredService<IDbContext>();

    public Task<IDataModel> LoadBrowseModel(String? dataSource)
    {
        var table = TableMetadataDefaults.OperationsTable();
        var sql = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        select [{table.CollectionName}!{table.TypeName}!Array] = null,
          [Id!!Id] = a.[Id], [Name!!Name] = a.[Name], a.[Memo]
        from {table.SqlTableName} a
        order by a.[Name];
        """;
        return _dbContext.LoadModelSqlAsync(dataSource, sql, _ => { });
    }

    /* Type-ahead in a selector pointing here. The shape is the one SqlBuilder.FetchAsync returns,
     * minus everything the registry has no room for: no 'Void' (the table has none) and no extra
     * columns, because Id and Name are all it holds worth carrying.
     */
    public async Task<IInvokeResult> FetchAsync(String? dataSource, ExpandoObject? prms)
    {
        var table = TableMetadataDefaults.OperationsTable();
        var sql = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        declare @fr nvarchar(255) = N'%' + @Text + N'%';

        select top(100) [{table.CollectionName}!{table.TypeName}!Array] = null,
          [Id!!Id] = a.[Id], [Name!!Name] = a.[Name]
        from {table.SqlTableName} a
        where a.[Name] like @fr
        order by a.[Name];
        """;
        var model = await _dbContext.LoadModelSqlAsync(dataSource, sql,
            dbprms => dbprms.AddString("@Text", prms?.Get<String>("Text")));
        return model.ToInvokeResult();
    }
}

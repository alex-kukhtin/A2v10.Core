// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    internal Task<IInvokeResult> FetchFolderAsync(ExpandoObject? prms)
    {
        throw new InvalidOperationException("IMPLEMENT FETCH FOLDER");
    }

    /* Which columns beyond Id and Name the answer has to carry, asked for by the caller.
     *
     * It has to be asked for: this endpoint is the catalog, and the catalog does not know who is
     * picking from it. 'inherit' is declared on the ROW that holds the reference, and the same
     * catalog is picked from by rows that inherit different things or nothing at all.
     *
     * What travels is the name of the column HERE - InheritDescriptor.Source - not the name of
     * the field it lands in over there. The two coincide often enough to be a trap.
     *
     * The list arrives from the browser, so it is resolved against the shape and never reaches
     * the query as text. That also gives the right answer when a generated view outlives the
     * column it names: a message, not a silently missing field.
     */
    private TableColumn[] FetchColumns(String? inherit)
    {
        if (String.IsNullOrEmpty(inherit))
            return [];
        return [.. inherit
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .Select(name => Table.Columns.FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException(
                    $"fetch: column '{name}' not found in {Table.SqlTableName}"))];
    }

    /* A reference among them is resolved the way the index resolves its own - RefId in the row,
     * the object itself in a Map recordset - and not as a nested join, because the object the
     * selector hands to the row has to have the same shape as the one the document load hands
     * to it. One shape, one place that decides it.
     */
    private static String FetchMaps(List<TableColumn> refColumns)
    {
        return String.Join("\n\n", refColumns
            .GroupBy(c => c.RefTableCheck.Storage.TypeName)
            .Select(g =>
            {
                var refTable = g.First().RefTableCheck.Storage;
                var ids = String.Join("\n    union all\n    ", g
                    .Select(c => $"select id = [{c.Name}] from @map where [{c.Name}] is not null group by [{c.Name}]"));
                return $"""
                with T as (
                    {ids}
                )
                select [!{g.Key}!Map] = null, [Id!!Id] = a.Id, [Name!!Name] = a.[Name]
                from {refTable.SqlTableName} a inner join T on a.Id = T.id;
                """;
            }));
    }

    internal async Task<IInvokeResult> FetchAsync(ExpandoObject? prms)
    {
        var columns = FetchColumns(prms?.Get<String>("inherit"));
        var extra = String.Join("", columns
            .Select(c => $",\n    {c.SqlModelColumnName("a", t => t.TypeName)}"));

        var refColumns = columns.Where(c => c.IsRef).ToList();

        /* Two shapes, and the difference is real: without references there is nothing to resolve
         * and one select answers; with them the same hundred rows are needed twice, so they are
         * materialized once instead of being selected twice under a LIKE.
         */
        var sql = refColumns.Count == 0
            ? $"""
            set nocount on;
            set transaction isolation level read uncommitted;

            declare @fr nvarchar(255);
            set @fr = N'%' + @Text + N'%';

            select top(100) [{Table.CollectionName}!{Table.TypeName}!Array] = null,
                [Id!!Id] = a.Id, [Name!!Name] = a.[Name]{extra}
            from {Table.SqlTableName} a
            where a.[Void] = 0 and
                (a.[Name] like @fr)
            order by a.[Name];
            """
            : $"""
            set nocount on;
            set transaction isolation level read uncommitted;

            declare @fr nvarchar(255);
            set @fr = N'%' + @Text + N'%';

            declare @map table(Id platformid, {String.Join(", ", refColumns.Select(c => $"[{c.Name}] {c.SqlDataType()}"))});

            insert into @map(Id, {String.Join(", ", refColumns.Select(c => $"[{c.Name}]"))})
            select top(100) a.Id, {String.Join(", ", refColumns.Select(c => $"a.[{c.Name}]"))}
            from {Table.SqlTableName} a
            where a.[Void] = 0 and
                (a.[Name] like @fr)
            order by a.[Name];

            select [{Table.CollectionName}!{Table.TypeName}!Array] = null,
                [Id!!Id] = a.Id, [Name!!Name] = a.[Name]{extra}
            from {Table.SqlTableName} a
                inner join @map m on m.Id = a.Id
            order by a.[Name];

            {FetchMaps(refColumns)}
            """;

        var model = await _dbContext.LoadModelSqlAsync(DataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Text", prms?.Get<String>("Text"));
        });

        return model.ToInvokeResult();
    }
}

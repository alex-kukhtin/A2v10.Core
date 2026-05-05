// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    public async Task<IDataModel> LoadIndexModelAsync(Boolean lazy = false)
    {
        const String DEFAULT_DIR = "desc";
        String defaultOrder = "Id";

        var qry = _descr.PlatformUrl.Query;
        Int32 offset = 0;
        Int32 pageSize = 20;
        String? fragment = null;
        String order = defaultOrder;
        String dir = DEFAULT_DIR;

        var collectionName = _descr.Table.CollectionName;

        if (qry != null)
        {
            if (qry.HasProperty("Offset"))
                offset = Int32.Parse(qry.Get<String>("Offset") ?? "0");
            if (qry.HasProperty("PageSize"))
                pageSize = Int32.Parse(qry.Get<String>("PageSize") ?? "20");
            fragment = qry?.Get<String?>("Fragment");
            order = qry?.Get<String>("Order") ?? defaultOrder;
            dir = qry?.Get<String>("Dir")?.ToLowerInvariant() ?? DEFAULT_DIR;
        }
        if (dir != "asc" && dir != "desc")
            dir = DEFAULT_DIR;
        // TODO: Ensure Order is valid

        var refFields = RefFields;

        ReferenceMember? FindReference(TableColumn column)
        {
            if (column.Type != ColumnType.Ref)
                return null;
            return RefFields.FirstOrDefault(r => column.Target == r.Table.Path);
        }

        IEnumerable<String> indexSqlFields(String alias)
        {
            foreach (var c in _descr.Table.DefaultColumns())
                yield return c.SqlModelColumnName(alias, FindReference(c));
            foreach (var c in _descr.Table.Columns)
            {
                yield return c.SqlModelColumnName(alias, FindReference(c));
            }
        }


        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        
        set @Dir = lower(@Dir);
        declare @fr nvarchar(255) = N'%' + @Fragment + N'%';

        declare @map table (rowNo int identity(1,1), Id bigint, {RefFieldsEnum(r => $"{r.Column.Name} bigint")} rowCnt int);

        insert into @map(a.Id, {RefFieldsEnum(r => $"{r.Column.Name}")} rowCnt)
        select a.Id, {RefFieldsEnum(r => $"a.{r.Column.Name}")}
            [!!RowCount]  = count(*) over()        
        from {_descr.Table.SqlTableName} a
        where a.Void = 0 -- TODO: where clause
        order by a.[{order}] {dir}
        offset @Offset rows fetch next @PageSize rows only option(recompile);
        
        select [{collectionName}!{_descr.Table.TypeName}!Array] = null, [Id!!Id] = a.Id,
            {String.Join(", ", indexSqlFields("a"))},
            [!!RowCount]  = t.rowCnt        
        from {_descr.Table.SqlTableName} a
            inner join @map t on t.Id = a.Id
        order by t.rowNo;

        {RefFieldsMap()}

        -- system data
        select [!$System!] = null,
        	[!{collectionName}!PageSize] = @PageSize,  [!{collectionName}!Offset] = @Offset,
        	[!{collectionName}!SortOrder] = @Order,  [!{collectionName}!SortDir] = @Dir,
        	[!{collectionName}.Fragment!Filter] = @Fragment;
        """;

        return await _dbContext.LoadModelSqlAsync(_descr.DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            AddPeriodParameters(dbprms, qry);

            if (lazy)
                dbprms.AddString("@Id", _descr.PlatformUrl.Id);

            dbprms.AddInt("@Offset", offset)
            .AddInt("@PageSize", pageSize)
            .AddString("@Order", order)
            .AddString("@Dir", dir)
            .AddString("@Fragment", fragment);
        });
    }
}

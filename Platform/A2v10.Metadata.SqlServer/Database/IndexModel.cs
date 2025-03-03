// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata.SqlServer;

internal partial class DatabaseModelProcessor
{
    public Task<IDataModel> LoadIndexModelAsync(TableMetadata meta, IPlatformUrl platformUrl, IModelView view)
    {
        var viewMeta = view.Meta ??
            throw new InvalidOperationException($"view.Meta is null");

        const String DEFAULT_DIR = "asc";
        if (meta.Columns.Count == 0)
            throw new InvalidOperationException($"The model '{viewMeta.Table}' does not have columns");


        String defaultOrder = meta.Columns[0].Name;

        var qry = platformUrl.Query;
        Int32 offset = 0;
        Int32 pageSize = 20;
        String? fragment = null;
        String order = defaultOrder;
        String dir = DEFAULT_DIR;
        if (qry != null)
        {
            if (qry.HasProperty("Offset"))
                offset = Int32.Parse(qry.Get<String>("Offset") ?? "0");
            if (qry.HasProperty("PageSize"))
                pageSize = Int32.Parse(qry.Get<String>("PageSize") ?? "20");
            fragment = qry?.Get<String>("Fragment");
            order = qry?.Get<String>("Order") ?? defaultOrder;
            dir = qry?.Get<String>("Dir")?.ToLowerInvariant() ?? DEFAULT_DIR;
        }

        String RefTableFields()
        {
            return String.Empty;
        }

        String RefInsertFields() { 
            return String.Empty; 
        }

        String ParametersCondition() { 
            return $"a.[{viewMeta.Void}] = 0"; 
        }

        String WhereCondition()
        {
            return String.Empty;
        }

        String SelectFieldsAll(TableMetadata meta, String alias) =>
            String.Join(' ', meta.RealColumns(viewMeta).Select(f => $"{f.SqlFieldName(alias)},"));

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        
        set @Dir = lower(@Dir);
        
        declare @fr nvarchar(255) = N'%' + @Fragment + N'%';
        
        declare @tmp table(Id bigint, rowno int identity(1, 1), {RefTableFields()} rowcnt int);
        insert into @tmp(Id, {RefInsertFields()} rowcnt)
        select a.Id, {RefInsertFields()} count(*) over() 
        from {meta.SqlTableName} a
        where {ParametersCondition()} {WhereCondition()}
        order by a.[{order}] {dir}
        offset @Offset rows fetch next @PageSize rows only option (recompile);
        
        select [{meta.Table}!{meta.ModelType}!Array] = null,
            {SelectFieldsAll(meta, "a")}
            [!!RowCount]  = t.rowcnt        
        from @tmp t inner join {meta.SqlTableName} a on t.[Id] = a.[Id]
        order by rowno;

        -- system data
        select [!$System!] = null,
        	[!{meta.Table}!PageSize] = @PageSize,  [!{meta.Table}!Offset] = @Offset,
        	[!{meta.Table}!SortOrder] = @Order,  [!{meta.Table}!SortDir] = @Dir,
        	[!{meta.Table}.Fragment!Filter] = @Fragment;
        
        """;
        return _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddInt("@Offset", offset);
            dbprms.AddInt("@PageSize", pageSize);
            dbprms.AddString("@Order", order);
            dbprms.AddString("@Dir", dir);
            dbprms.AddString("@Fragment", fragment);
        });
    }
}

// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;

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
            fragment = qry?.Get<String?>("Fragment");
            order = qry?.Get<String>("Order") ?? defaultOrder;
            dir = qry?.Get<String>("Dir")?.ToLowerInvariant() ?? DEFAULT_DIR;
        }

        var refFields = meta.RefFields(viewMeta);

        var sqlOrder = $"a.[{order}]";
        var sortColumn = refFields.FirstOrDefault(c => c.Column.Name == order);

        if (sortColumn.Column != null)
            sqlOrder = $"r{sortColumn.Index}.[Name]";

        String ParametersCondition() { 
            return $"a.[{viewMeta.Void}] = 0"; 
        }

        String WhereCondition()
        {
            if (String.IsNullOrEmpty(fragment))
                return String.Empty;
            var searchable =
                meta.RealColumns(viewMeta).Where(c => c.IsSearchable).Select(c => $"a.[{c.Name}] like @fr")
                .Union(refFields.Select(c => $"r{c.Index}.[Name] like @fr"));
                
            return $"and (@fr is null or {String.Join(" or ", searchable)})";
        }

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        
        set @Dir = lower(@Dir);
        
        declare @fr nvarchar(255) = N'%' + @Fragment + N'%';
                
        select [{meta.Table}!{meta.ModelType}!Array] = null,
            {String.Join(",", meta.SelectFieldsAll("a", viewMeta, refFields))},
            [!!RowCount]  = count(*) over()        
        from {meta.SqlTableName} a
        {RefTableJoins(refFields)}
        where {ParametersCondition()} {WhereCondition()}
        order by {sqlOrder} {dir}
        offset @Offset rows fetch next @PageSize rows only option (recompile);
        
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

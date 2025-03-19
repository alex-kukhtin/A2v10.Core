// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

internal partial class DatabaseModelProcessor
{
    public Task<IDataModel> LoadIndexModelAsync(TableMetadata table, IPlatformUrl platformUrl, IModelView view, AppMetadata appMeta)
    {
        var viewMeta = view.Meta ??
            throw new InvalidOperationException($"view.Meta is null");

        const String DEFAULT_DIR = "asc";
        if (table.Columns.Count == 0)
            throw new InvalidOperationException($"The model '{viewMeta.Table}' does not have columns");


        String defaultOrder = table.Columns[0].Name;

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
        // TODO: order взять из таблицы - он попадает в SQL!

        var refFields = table.RefFields();

        var sqlOrder = $"a.[{order}]";
        var sortColumn = refFields.FirstOrDefault(c => c.Column.Name == order);

        if (sortColumn.Column != null)
            sqlOrder = $"r{sortColumn.Index}.[Name]"; // TODO: NameField

        String ParametersCondition() {
            return $"a.[{appMeta.VoidField}] = 0"; 
        }

        String WhereCondition()
        {
            if (String.IsNullOrEmpty(fragment))
                return String.Empty;
            var searchable = table
                .Columns.Where(c => c.IsSearchable).Select(c => $"a.[{c.Name}] like @fr")
                .Union(refFields.Select(c => $"r{c.Index}.[Name] like @fr"));
                
            return $"and (@fr is null or {String.Join(" or ", searchable)})";
        }

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        
        set @Dir = lower(@Dir);
        
        declare @fr nvarchar(255) = N'%' + @Fragment + N'%';
                
        select [{table.Name}!{table.ModelType}!Array] = null,
            {String.Join(",", table.AllSqlFields("a", appMeta))},
            [!!RowCount]  = count(*) over()        
        from {table.Schema}.[{table.Name}] a
        {RefTableJoins(refFields, appMeta)}
        where {ParametersCondition()} {WhereCondition()}
        order by {sqlOrder} {dir}
        offset @Offset rows fetch next @PageSize rows only option (recompile);
        
        -- system data
        select [!$System!] = null,
        	[!{table.Name}!PageSize] = @PageSize,  [!{table.Name}!Offset] = @Offset,
        	[!{table.Name}!SortOrder] = @Order,  [!{table.Name}!SortDir] = @Dir,
        	[!{table.Name}.Fragment!Filter] = @Fragment;
        
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

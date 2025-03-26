// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

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

        if (!table.Columns.Any(c => c.Name.Equals(order, StringComparison.OrdinalIgnoreCase))) 
            order = defaultOrder;

        if (dir != "asc" && dir != "desc")
            dir = DEFAULT_DIR;

        var refFields = table.RefFields();

        var sqlOrder = $"a.[{order}]";
        var sortColumn = refFields.FirstOrDefault(c => c.Column.Name == order);

        if (sortColumn.Column != null)
            sqlOrder = $"r{sortColumn.Index}.[Name]"; // TODO: NameField

        IEnumerable<String> Where()
        {
            yield return $"a.[{appMeta.VoidField}]=0";

            foreach (var r in refFields)
            {
                var val = qry?.GetInt64(r.Column.Name);
                if (val != null && val != 0)
                    yield return $"a.[{r.Column.Name}] = @{r.Column.Name}";
            }

            var searchable = table
                .Columns.Where(c => c.IsSearchable).Select(c => $"a.[{c.Name}] like @fr")
                .Union(refFields.Select(c => $"r{c.Index}.[Name] like @fr"));

            if (!String.IsNullOrEmpty(fragment))
                yield return $"(@fr is null or {String.Join(" or ", searchable)})";
        }

        String filterJoins() 
        { 
            if (!refFields.Any())
                return String.Empty;
            return " left join " + String.Join(" left join ", refFields.Select(r => 
                $"""
                {r.Column.Reference.RefSchema}.[{r.Column.Reference.RefTable}] r{r.Index} on r{r.Index}.[{appMeta.IdField}] = @{r.Column.Name}
                """));
        }

        String filterFields()
        {
            if (!refFields.Any())
                return String.Empty;
            return ", " + String.Join(", ", refFields.Select(r =>
            $"""
            [!{table.Name}.{r.Column.Name}.{appMeta.IdField}!Filter] = r{r.Index}.[{appMeta.IdField}],
            [!{table.Name}.{r.Column.Name}.{appMeta.NameField}!Filter] = r{r.Index}.[{appMeta.NameField}]
            """));
        }

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        
        set @Dir = lower(@Dir);
        
        declare @fr nvarchar(255) = N'%' + @Fragment + N'%';
                
        select [{table.Name}!{table.RealTypeName}!Array] = null,
            {String.Join(",", table.AllSqlFields("a", appMeta))},
            [!!RowCount]  = count(*) over()        
        from {table.Schema}.[{table.Name}] a
        {RefTableJoins(refFields, "a", appMeta)}
        where {String.Join(" and ", Where())}
        order by {sqlOrder} {dir}
        offset @Offset rows fetch next @PageSize rows only;
        
        declare @ft table(Id int);
        insert into @ft (Id) values (1);
        -- system data
        select [!$System!] = null,
        	[!{table.Name}!PageSize] = @PageSize,  [!{table.Name}!Offset] = @Offset,
        	[!{table.Name}!SortOrder] = @Order,  [!{table.Name}!SortDir] = @Dir,
        	[!{table.Name}.Fragment!Filter] = @Fragment{filterFields()}
        from @ft {filterJoins()};
        
        """;
        return _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddInt("@Offset", offset);
            dbprms.AddInt("@PageSize", pageSize);
            dbprms.AddString("@Order", order);
            dbprms.AddString("@Dir", dir);
            dbprms.AddString("@Fragment", fragment);
            foreach (var r in refFields)
                dbprms.AddBigInt($"@{r.Column.Name}", qry?.GetInt64(r.Column.Name));
        });
    }
}

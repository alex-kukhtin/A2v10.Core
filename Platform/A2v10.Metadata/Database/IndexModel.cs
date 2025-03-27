// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Data.SqlClient;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    public Task<IDataModel> LoadIndexModelAsync()
    {
        const String DEFAULT_DIR = "asc";
        if (_table.Columns.Count == 0)
            throw new InvalidOperationException($"The model '{_table.Name}' does not have columns");

        String defaultOrder = _table.Columns[0].Name;

        var qry = _platformUrl.Query;
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

        if (!_table.Columns.Any(c => c.Name.Equals(order, StringComparison.OrdinalIgnoreCase))) 
            order = defaultOrder;

        if (dir != "asc" && dir != "desc")
            dir = DEFAULT_DIR;

        var refFields = _table.RefFields();

        var sqlOrder = $"a.[{order}]";
        var sortColumn = refFields.FirstOrDefault(c => c.Column.Name == order);

        if (sortColumn.Column != null)
            sqlOrder = $"r{sortColumn.Index}.[Name]"; // TODO: NameField

        IEnumerable<String> Where()
        {
            yield return $"a.[{_appMeta.VoidField}]=0";

            foreach (var r in refFields)
            {
                var val = qry?.Get<Object>(r.Column.Name);
                if (val != null)
                    yield return $"a.[{r.Column.Name}] = @{r.Column.Name}";
            }

            var searchable = _table
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
                {r.Column.Reference.RefSchema}.[{r.Column.Reference.RefTable}] r{r.Index} on r{r.Index}.[{_appMeta.IdField}] = @{r.Column.Name}
                """));
        }

        String filterFields()
        {
            if (!refFields.Any())
                return String.Empty;
            return ", " + String.Join(", ", refFields.Select(r =>
            $"""
            [!{_table.Name}.{r.Column.Name}.{_appMeta.IdField}!Filter] = r{r.Index}.[{_appMeta.IdField}],
            [!{_table.Name}.{r.Column.Name}.{_appMeta.NameField}!Filter] = r{r.Index}.[{_appMeta.NameField}]
            """));
        }

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        
        set @Dir = lower(@Dir);
        
        declare @fr nvarchar(255) = N'%' + @Fragment + N'%';
                
        select [{_table.Name}!{_table.RealTypeName}!Array] = null,
            {String.Join(",", _table.AllSqlFields("a", _appMeta))},
            [!!RowCount]  = count(*) over()        
        from {_table.Schema}.[{_table.Name}] a
        {RefTableJoins(refFields, "a")}
        where {String.Join(" and ", Where())}
        order by {sqlOrder} {dir}
        offset @Offset rows fetch next @PageSize rows only;
        
        declare @ft table(Id int);
        insert into @ft (Id) values (1);
        -- system data
        select [!$System!] = null,
        	[!{_table.Name}!PageSize] = @PageSize,  [!{_table.Name}!Offset] = @Offset,
        	[!{_table.Name}!SortOrder] = @Order,  [!{_table.Name}!SortDir] = @Dir,
        	[!{_table.Name}.Fragment!Filter] = @Fragment{filterFields()}
        from @ft {filterJoins()};
        
        """;
        return _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddInt("@Offset", offset);
            dbprms.AddInt("@PageSize", pageSize);
            dbprms.AddString("@Order", order);
            dbprms.AddString("@Dir", dir);
            dbprms.AddString("@Fragment", fragment);
            foreach (var r in refFields)
                dbprms.Add(new SqlParameter($"@{r.Column.Name}", _appMeta.IdDataType.ToSqlDbType())
                {
                    Value = qry?.Get<Object>(r.Column.Name) ?? DBNull.Value
                });
        });
    }
}

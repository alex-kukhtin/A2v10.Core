// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    public Task<IDataModel> LoadIndexModelAsync()
    {
        const String DEFAULT_DIR = "asc";

        const String DATE_COLUMN = "Date"; //TODO: ???

        if (_table.Columns.Count == 0)
            throw new InvalidOperationException($"The model '{_table.Name}' does not have columns");

        TableColumn? opColumn = null;
        String? opValue = null;
        if (_baseTable != null && _baseTable.Schema == "op")
        {
            opColumn = _table.Columns.FirstOrDefault(c => c.DataType == ColumnDataType.Operation);
            opValue = _baseTable?.Name.ToLowerInvariant();
        }

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
        var refFieldsFilter = refFields;
        if (opColumn != null)
            refFieldsFilter = refFields.Where(c => c.Column != opColumn);

        var sqlOrder = $"a.[{order}]";
        var sortColumn = refFields.FirstOrDefault(c => c.Column.Name == order);

        if (sortColumn.Column != null)
            sqlOrder = $"r{sortColumn.Index}.[Name]"; // TODO: NameField

        var collectionName = _table.RealItemsName;

        IEnumerable<String> Where()
        {
            yield return "1 = 1"; // for default // TODO: ???? как-то проверить

            if (_table.Columns.Any(c => c.Name == _appMeta.VoidField))
                yield return $"a.[{_appMeta.VoidField}]=0";

            if (opColumn != null)
                yield return $"a.[{opColumn.Name}] = N'{opValue}'";

            if (_table.HasPeriod())
                yield return $"a.[{DATE_COLUMN}] >= @From and a.[{DATE_COLUMN}] <= @To";

            foreach (var r in refFieldsFilter)
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
            if (!refFieldsFilter.Any())
                return String.Empty;
            return " left join " + String.Join(" left join ", refFieldsFilter.Select(r => 
                $"""
                {r.Column.Reference.RefSchema}.[{r.Column.Reference.RefTable}] r{r.Index} on r{r.Index}.[{_appMeta.IdField}] = @{r.Column.Name}
                """));
        }


        String filterFields()
        {
            if (!refFieldsFilter.Any())
                return String.Empty;
            return ", " + String.Join(", ", refFieldsFilter.Select(r =>
            $"""
            [!{collectionName}.{r.Column.Name}.{_appMeta.IdField}!Filter] = r{r.Index}.[{_appMeta.IdField}],
            [!{collectionName}.{r.Column.Name}.{_appMeta.NameField}!Filter] = r{r.Index}.[{_appMeta.NameField}]
            """));
        }

        String filterPeriod()
        {
            if (!_table.HasPeriod())
                return String.Empty;
            return $"[!{collectionName}.Period.From!Filter] = @From, [!{collectionName}.Period.To!Filter] = @To,";
        }

        String defaultPeriod()
        {
            if (!_table.HasPeriod())
                return String.Empty;

            return "set @From = isnull(@From, N'19010101'); set @To = isnull(@To, N'29991231');";
        }

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        set @Dir = lower(@Dir);
        {defaultPeriod()}
        declare @fr nvarchar(255) = N'%' + @Fragment + N'%';
                
        select [{collectionName}!{_table.RealTypeName}!Array] = null,
            {String.Join(",", _table.AllSqlFields("a", _appMeta))},
            [!!RowCount]  = count(*) over()        
        from {_table.SqlTableName} a
        {RefTableJoins(refFields, "a")}
        where {String.Join(" and ", Where())}
        order by {sqlOrder} {dir}
        offset @Offset rows fetch next @PageSize rows only;
        
        declare @ft table(Id int);
        insert into @ft (Id) values (1);
        -- system data
        select [!$System!] = null,
        	[!{collectionName}!PageSize] = @PageSize,  [!{collectionName}!Offset] = @Offset,
        	[!{collectionName}!SortOrder] = @Order,  [!{collectionName}!SortDir] = @Dir,
            {filterPeriod()}
        	[!{collectionName}.Fragment!Filter] = @Fragment{filterFields()}
        from @ft {filterJoins()};
        
        """;
        return _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            AddPeriodParameters(dbprms, qry);
            dbprms.AddInt("@Offset", offset)
            .AddInt("@PageSize", pageSize)
            .AddString("@Order", order)
            .AddString("@Dir", dir)
            .AddString("@Fragment", fragment);
            foreach (var r in refFieldsFilter)
                dbprms.AddTyped($"@{r.Column.Name}", r.Column.DataType.ToSqlDbType(_appMeta.IdDataType), qry?.Get<Object>(r.Column.Name));
        });
    }
}

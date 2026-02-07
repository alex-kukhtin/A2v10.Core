// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    public async Task<IDataModel> LoadIndexModelAsync(Boolean lazy = false)
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

        var refFields = _refFields; // await ReferenceFieldsAsync(_table);
        var refFieldsFilter = refFields;

        if (opColumn != null)
            refFieldsFilter = refFields.Where(c => c.Column != opColumn);

        var sqlOrder = $"a.[{order}]";
        var sortColumn = refFields.FirstOrDefault(c => c.Column.Name == order);

        var bitFields = _table.Columns.Where(c => c.IsBitField);

        if (sortColumn?.Column != null)
            sqlOrder = $"r{sortColumn.Index}.[{sortColumn.Table.NameField}]";

        var collectionName = _table.RealItemsName;
        var enumFields = DatabaseMetadataProvider.EnumFields(_table, false);

        IEnumerable<String> WhereClause()
        {
            yield return "1 = 1"; // for default // TODO: ???? как-то проверить

            if (lazy)
            {
                yield return $"a.{_table.IsFolderField} = 0"; // for lazy loading
                yield return $"(@Id = 0 or a.{_table.ParentField} = @Id or (@Id = -2 and a.{_table.ParentField} is null))";
            }

            if (_table.Columns.Any(c => c.Role.HasFlag(TableColumnRole.Void)))
                yield return $"a.[{_table.VoidField}] = 0";

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

            foreach (var e in enumFields)
            {
                var val = qry?.Get<String>(e.Column.Name);
                if (!String.IsNullOrEmpty(val))
                    yield return $"a.[{e.Column.Name}] = @{e.Column.Name}";
            }

            foreach (var b in bitFields)
            {
                var val = qry?.Get<String>(b.Name);
                if (!String.IsNullOrEmpty(val))
                {
                    if (val == "Y")
                        yield return $"a.[{b.Name}] = 1";
                    else if (val == "N")
                        yield return $"a.[{b.Name}] = 0 or a.[{b.Name}] is null";
                }
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
                {r.Table.SqlTableName} r{r.Index} on r{r.Index}.[{r.Table.PrimaryKeyField}] = @{r.Column.Name}
                """));
        }


        String filterFields()
        {
            // references, bit, enum
            var filterFields = refFieldsFilter.Select(r =>
            $"""
            [!{collectionName}.{r.Column.Name}.{r.Table.PrimaryKeyField}!Filter] = r{r.Index}.[{r.Table.PrimaryKeyField}],
            [!{collectionName}.{r.Column.Name}.{r.Table.NameField}!Filter] = r{r.Index}.[{r.Table.NameField}]
            """);
            var enumFilterFields = enumFields.Select(e => 
                $"[!{collectionName}.{e.Column.Name}.TR{e.Table.RealItemName}.RefId!Filter] = @{e.Column.Name}");

            var bitFiltersFields = bitFields.Select(b => 
                $"[!{collectionName}.{b.Name}!Filter] = @{b.Name}");

            filterFields = filterFields.Union(enumFilterFields).Union(bitFiltersFields);
            if (!filterFields.Any())
                return String.Empty;
            return ", " + String.Join(", ", filterFields);
        }


        String filterPeriod()
        {
            if (!_table.HasPeriod())
                return String.Empty;
            return $"[!{collectionName}.Period.From!Filter] = @From, [!{collectionName}.Period.To!Filter] = @To,";
        }

        String filterEnumCheck()
        {
            var sb = new StringBuilder();
            foreach (var r in enumFields)
                sb.AppendLine($"set @{r.Column.Name} = isnull(@{r.Column.Name}, N'');");
            foreach (var e in enumFields)
                sb.AppendLine($"set @{e.Column.Name} = isnull(@{e.Column.Name}, N'');");
            return sb.ToString();
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
            {String.Join(",", _table.AllSqlFields(refFields, enumFields, "a"))},
            [!!RowCount]  = count(*) over()        
        from {_table.SqlTableName} a
        {RefTableJoins(refFields, "a")}
        where {String.Join(" and ", WhereClause())}
        order by {sqlOrder} {dir}
        offset @Offset rows fetch next @PageSize rows only;
        
        {EnumsMapSql(enumFields, true)}
        
        -- After select, before $System.
        {filterEnumCheck()}
        
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
        return await _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            AddPeriodParameters(dbprms, qry);

            if (lazy)
                dbprms.AddString("@Id", _platformUrl.Id);

            dbprms.AddInt("@Offset", offset)
            .AddInt("@PageSize", pageSize)
            .AddString("@Order", order)
            .AddString("@Dir", dir)
            .AddString("@Fragment", fragment);
            foreach (var r in refFieldsFilter)
            {
                var data = qry?.Get<Object>(r.Column.Name);
                if (data != null && _appMeta.IdDataType == ColumnDataType.Uniqueidentifier)
                    data = Guid.Parse(data.ToString()!);
                dbprms.AddTyped($"@{r.Column.Name}", r.Column.DataType.ToSqlDbType(_appMeta.IdDataType), data);
            }
            foreach (var e in enumFields)
            {
                var val = qry?.Get<String>(e.Column.Name);
                dbprms.AddString($"@{e.Column.Name}", val ?? "");
            }
            foreach (var b in bitFields)
            {
                var val = qry?.Get<String>(b.Name);
                dbprms.AddString($"@{b.Name}", val ?? ""); // all values
            }
        });
    }
}

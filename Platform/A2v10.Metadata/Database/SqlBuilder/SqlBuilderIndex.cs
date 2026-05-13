// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    // TODO: SEARCH BY ID
    public async Task<IDataModel> LoadIndexModelAsync(Boolean lazy = false)
    {
        const String DEFAULT_DIR = "desc";
        const Int32 DEFAULT_PAGE_SIZE = 20;

        Int32 offset = 0;
        Int32 pageSize = DEFAULT_PAGE_SIZE;
        String? fragment = null;
        
        (String field, String value, RefDescriptor? refdescr) = ("a.Id", "Id", null);

        String dir = DEFAULT_DIR;
        List<(String name, String value)> filters = [];

        var qry = _descr.PlatformUrl.Query;

        var allColumns = Table.AllColumns().ToList();
        var refs = allColumns.AllRefs().ToList();

        // parse query
        if (qry != null)
        {
            if (qry.HasProperty("Offset"))
                if (!Int32.TryParse(qry.Get<String>("Offset") ?? "0", out offset))
                    offset = 0;
            if (qry.HasProperty("PageSize"))
                if (!Int32.TryParse(qry.Get<String>("PageSize") ?? DEFAULT_PAGE_SIZE.ToString(), out pageSize))
                    pageSize = DEFAULT_PAGE_SIZE;
            fragment = qry.Get<String?>("Fragment");
            dir = qry.Get<String>("Dir")?.ToLowerInvariant() ?? DEFAULT_DIR;
            if (dir != "asc" && dir != "desc")
                dir = DEFAULT_DIR;
            var queryOrder = qry.Get<String>("Order");

            var orderColumn = allColumns.FirstOrDefault(c => c.Name.Equals(queryOrder, StringComparison.OrdinalIgnoreCase));
            if (orderColumn != null)
            {
                var rd = refs.FirstOrDefault(r => r.Column == orderColumn);
                if (rd != null)
                {
                    field = $"r{rd.Index}.[{rd.Column.Presentation}]";
                    value = rd.Column.Name;
                    refdescr = rd;
                }
                else
                {
                    field = $"a.{orderColumn.Name}";
                    value = orderColumn.Name;
                }
            }

            foreach (var (index, column, table) in refs)
            {
                var f = qry.Get<Object>(column.Name) ?? qry.Get<Object>(column.Name.ToLowerInvariant());
                if (f != null)
                    filters.Add((column.Name, f.ToString()!));
            }
        }

        var collectionName = Table.CollectionName;

        String buildIndexSql()
        {
            var sb = new StringBuilder($"""
            -- index for {Table.Model}

            set nocount on;
            set transaction isolation level read uncommitted;
            """);
            sb.AppendLine();

            // STEP 1: prepare filters
            if (!String.IsNullOrEmpty(fragment))
            {
                sb.AppendLine();
                sb.AppendLine("declare @fr nvarchar(255) = N'%' + @Fragment + N'%';");
            }

            // STEP 2: create temp table
            sb.AppendLine();
            sb.AppendLine("-- map table");
            sb.Append("declare @map table(rowNo int identity(1,1), rowCnt int, Id bigint");
            if (refs.Count > 0)
            {
                sb.Append(", ");
                sb.Append(String.Join(", ", refs.Select(c => $"[{c.Column.Name}] bigint")));
            }
            sb.AppendLine(");");

            // STEP 3: main insert into select
            sb.AppendLine();
            sb.AppendLine("-- main insert");
            sb.Append("insert into @map(Id, rowCnt");
            if  (refs.Count > 0)
            {
                sb.Append(", ");
                sb.Append(String.Join(", ", refs.Select(c => $"a.[{c.Column.Name}]")));
            }
            sb.AppendLine(")");
            sb.Append("select a.Id, count(*) over()");
            if (refs.Count > 0)
            {
                sb.Append(", ");
                sb.Append(String.Join(", ", refs.Select(c => $"a.[{c.Column.Name}]")));
            }
            sb.AppendLine();
            sb.AppendLine($"from {Table.SqlTableName} a");
            if (!String.IsNullOrEmpty(fragment))
            {
                // find always
                foreach (var (index, column, table) in refs)
                    sb.AppendLine($"  left join {table.SqlTableName} r{index} on r{index}.Id = a.[{column.Name}]");
            }
            else if (refdescr != null)
            {
                // order by for one table only
                sb.AppendLine($"  left join {refdescr.Table.SqlTableName} r{refdescr.Index} on r{refdescr.Index}.Id = a.[{refdescr.Column.Name}]");
            }
            sb.Append("where a.Void = 0");

            if (filters.Count > 0)
                sb.AppendLine($" and {String.Join(" and ", filters.Select(f => $"a.[{f.name}] = @{f.name}"))}");
            if (!String.IsNullOrEmpty(fragment))
            {
                var searchColumns = allColumns.Where(c => c.IsSearchable).Select(x => $"a.[{x.Name}] like @fr")
                    .Concat(refs.Select(r => $"r{r.Index}.[{r.Column.Presentation}] like @fr")).ToList();
                if (searchColumns.Count > 0)
                {
                    sb.Append($" and ({String.Join(" or ", searchColumns)})");
                    sb.AppendLine();
                }
            }
            sb.AppendLine($"order by {field} {dir}");
            sb.AppendLine("offset @Offset rows fetch next @PageSize rows only option(recompile);");
            sb.AppendLine();

            // STEP 4: result recordest
            sb.AppendLine("-- result recordset");
            sb.AppendLine($"""
            select [{collectionName}!{Table.TypeName}!Array] = null, [!!RowCount]  = t.rowCnt,
              {String.Join(", ", indexSqlFields("a"))}
            from {Table.SqlTableName} a
              inner join @map t on t.Id = a.Id
            order by t.rowNo;
            """);
            // STEP 5: map recordsets
            if (refs.Count > 0)
            {
                sb.AppendLine();
                if (filters.Count > 0)
                {
                    foreach (var f in filters)
                        sb.Append($"insert into @map([{f.name}]) values (@{f.name});");
                    sb.AppendLine();
                }
                sb.AppendLine("-- map recordsets");
                var groupTables = refs.GroupBy(x => x.Table.Table).ToList();
                foreach (var gt in groupTables)
                {
                    if (gt.Count() == 1)
                    {
                        var gx = gt.First();
                        sb.AppendLine($"""
                        with TR as (
                          select Id = [{gx.Column.Name}] from @map where [{gx.Column.Name}] is not null group by [{gx.Column.Name}]
                        )
                        select [!{gx.Table.RefTypeName}!Map] = null, [Id!!Id] = r.Id, [{gx.Column.Presentation}!!Name] = r.[{gx.Column.Presentation}]
                        from {gx.Table.SqlTableName} r inner join TR on r.Id = TR.Id;
                        """);
                        sb.AppendLine();
                    }
                    else if (gt.Count() > 1)
                    {
                        throw new NotImplementedException("multiply map");
                    }
                }
            }

            // STEP 6: system recorset (filters -> always!)
            sb.AppendLine();
            sb.AppendLine("-- system recordset");
            sb.Append($"""
            select [!$System!] = null,
              [!{collectionName}!PageSize] = @PageSize,  [!{collectionName}!Offset] = @Offset,
              [!{collectionName}!SortOrder] = @Order,  [!{collectionName}!SortDir] = @Dir,
              [!{collectionName}.Fragment!Filter] = @Fragment
            """);
            if (refs.Count > 0) {
                sb.Append(", ");
                sb.Append(String.Join(", ", refs.Select(rt => $"[!{collectionName}.{rt.Column.Name}.{rt.Table.RefTypeName}.RefId!Filter] = @{rt.Column.Name}")));
            }
            sb.AppendLine(";");
            return sb.ToString();
        }


        IEnumerable<String> indexSqlFields(String alias)
        {
            static Boolean includeColumn(TableColumn col)
                => col.Type != ColumnType.RowVersion && col.Type != ColumnType.Void;
            return Table.AllColumns(includeColumn).Select(col => col.SqlModelColumnName(alias, t => t.RefTypeName));
        }

        var sqlQuery = buildIndexSql();

        // Console.WriteLine(sqlQuery);    

        return await _dbContext.LoadModelSqlAsync(_descr.DataSource, sqlQuery, dbprms =>
        {
            AddDefaultParameters(dbprms);
            AddPeriodParameters(dbprms, qry);

            if (lazy)
                dbprms.AddString("@Id", _descr.PlatformUrl.Id);

            dbprms.AddInt("@Offset", offset)
            .AddInt("@PageSize", pageSize)
            .AddString("@Order", value)
            .AddString("@Dir", dir)
            .AddString("@Fragment", fragment);
            foreach (var rd in refs)
            {
                Int64? paramValue = null;
                var val = filters.FirstOrDefault(f => f.name == rd.Column.Name);
                if (!String.IsNullOrEmpty(val.value) && Int64.TryParse(val.value, out var fval))
                    paramValue = fval;
                dbprms.AddBigInt($"@{rd.Column.Name}", paramValue);
            }
        });
    }
}

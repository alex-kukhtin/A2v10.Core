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
        // ids joined by '-', split in SQL; empty means the filter is off and nothing is emitted
        String? tags = null;

        (String field, String value, RefDescriptor? refdescr) = ("a.Id", "Id", null);

        String dir = DEFAULT_DIR;
        List<(String name, String value)> filters = [];

        var qry = _descr.PlatformUrl.Query;

        var allColumns = Table.AllColumns().ToList();
        var refs = allColumns.AllRefs().ToList();

        /* Searchable references only. A set is not one: its Name is a localization key, so a
         * fragment would be matched against '@[VatRate.20]' - the English code is findable, the
         * translation the user is actually reading is not. The join has no other purpose here, so
         * it goes with the predicate rather than staying as a cost with no function.
         */
        var searchRefs = refs.Where(r => !r.Column.IsEnum).ToList();

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
            if (Table.HasTags)
                tags = qry.Get<String?>(Constants.FilterNames.Tags);
            dir = qry.Get<String>("Dir")?.ToLowerInvariant() ?? DEFAULT_DIR;
            if (dir != "asc" && dir != "desc")
                dir = DEFAULT_DIR;
            var queryOrder = qry.Get<String>("Order");

            /* An enum column is not sortable at all, and the header offers no sort on it. Both
             * candidates lie about what the column shows: 'Order' is the order the set was
             * declared in, 'Name' is a localization key, and the cell displays the translation of
             * that key - so neither is the alphabet the reader sees. An unsortable name falls back
             * to the default, exactly like an unknown one.
             */
            var orderColumn = allColumns.FirstOrDefault(c =>
                c.Name.Equals(queryOrder, StringComparison.OrdinalIgnoreCase) && !c.IsEnum);
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

        String buildWhereClause()
        {
            var sb = new StringBuilder();

            var hasVoid = allColumns.Any(x => x.IsVoid);

            if (hasVoid)
                sb.Append("where a.Void = 0");
            else
                sb.Append("where 1 = 1"); // TODO:!!!!

            var docOp = Endpoint.DocumentOperation();
            if (docOp != null)
                sb.Append($"and a.[Operation] = @RouteOperation");

            if (Table.HasPeriod)
                sb.AppendLine(" and a.[Date] >= @From and a.[Date] < @end");

            // a row passes when it carries any of the picked tags; nothing picked, nothing emitted
            if (!String.IsNullOrEmpty(tags))
                sb.AppendLine($$"""
                 and exists(select 1 from @ftags f
                    inner join {{TableMetadataDefaults.TagEntriesTableName(Table.Model)}} ta
                        on ta.[Owner] = a.Id and ta.[Tag] = f.Id)
                """);

            /* An enum filter has a value meaning 'no restriction' - the set's own 'All' row, whose
             * key is the empty string. Nothing else in the platform has such a value, which is why
             * this predicate is not the general one. Null is not among the cases: it was turned
             * into the empty string at the top, in one place, before anything reads the parameter.
             */
            String filterPredicate((String name, String value) f) =>
                allColumns.FirstOrDefault(c => c.Name == f.name)?.IsEnum == true
                    ? $"(@{f.name} = N'' or a.[{f.name}] = @{f.name})"
                    : $"a.[{f.name}] = @{f.name}";

            if (filters.Count > 0)
                sb.AppendLine($" and {String.Join(" and ", filters.Select(filterPredicate))}");
            if (!String.IsNullOrEmpty(fragment))
            {
                var searchColumns = allColumns.Where(c => c.IsSearchable).Select(x => $"a.[{x.Name}] like @fr")
                    .Concat(searchRefs.Select(r => $"r{r.Index}.[{r.Column.Presentation}] like @fr")).ToList();
                if (searchColumns.Count > 0)
                {
                    sb.Append($" and ({String.Join(" or ", searchColumns)})");
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

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

            if (Table.HasPeriod)
            {
                sb.AppendLine();
                sb.AppendLine("set @From = isnull(@From, getdate());");
                sb.AppendLine("set @To = isnull(@To, getdate());");
                sb.AppendLine("declare @end date = dateadd(day, 1, @To)");
            }

            /* An enum filter has no state between 'nothing was sent' and 'everything': the set
             * carries an 'All' row of its own and its key is the empty string. Normalized once,
             * here, so that the WHERE, the map insert and the Filter that goes back all read the
             * same value - a null reaching the control would match no item in the list and leave
             * the ComboBox blank on the first load. Same as the hand-written '@X nvarchar(64) = N'''.
             */
            foreach (var en in refs.Where(r => r.Column.IsEnum))
            {
                sb.AppendLine();
                sb.AppendLine($"set @{en.Column.Name} = isnull(@{en.Column.Name}, N'');");
            }

            // CAST takes system types only, so it names the base the database reported for
            // 'platformid' - a hardcoded bigint would drop every tag on a uniqueidentifier base
            if (!String.IsNullOrEmpty(tags))
            {
                sb.AppendLine();
                sb.AppendLine("declare @ftags table(Id platformid);");
                sb.AppendLine("insert into @ftags(Id) select try_cast([value] as "
                    + $"{_descr.PlatformId.SqlTypeName}) "
                    + $"from string_split(@{Constants.FilterNames.Tags}, N'-');");
            }

            // STEP 2: create temp table
            sb.AppendLine();
            sb.AppendLine("-- map table");
            sb.Append("declare @map table(rowNo int identity(1,1), rowCnt int, Id platformid");
            if (refs.Count > 0)
            {
                sb.Append(", ");
                sb.Append(String.Join(", ", refs.Select(c => $"[{c.Column.Name}] {c.Column.SqlDataType()}")));
            }

            sb.AppendLine(");");

            // STEP 3: main insert into select
            sb.AppendLine();
            sb.AppendLine("-- main insert");
            sb.Append("insert into @map(Id, rowCnt");
            if  (refs.Count > 0)
            {
                sb.Append(", ");
                sb.Append(String.Join(", ", refs.Select(c => $"[{c.Column.Name}]")));
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
                // find always. The sorted-on reference is among these: an enum, the one kind that is
                // not searchable, is not sortable either, so no ORDER BY names an alias missing here
                foreach (var (index, column, table) in searchRefs)
                    sb.AppendLine($"  left join {table.SqlTableName} r{index} on r{index}.Id = a.[{column.Name}]");
            }
            else if (refdescr != null)
            {
                // order by for one table only
                sb.AppendLine($"  left join {refdescr.Table.SqlTableName} r{refdescr.Index} on r{refdescr.Index}.Id = a.[{refdescr.Column.Name}]");
            }

            sb.AppendLine(buildWhereClause());

            /* The tiebreaker is what makes paging deterministic. OFFSET/FETCH re-runs the whole
             * query for every page, and rows equal on the sort key have no order between two
             * executions - so one row lands on two pages and another on none, with the data
             * unchanged. option(recompile) below makes that MORE likely, not less: consecutive
             * pages are compiled separately.
             *
             * It must be unique per row of the RESULT, and the result is rows of 'a'. Not the
             * joined ref's Id, and not the foreign key beside it: both are one value inside a tie
             * group by construction (r.Id = a.[Col]), so neither splits anything. There is exactly
             * one such column, so the tiebreaker is the same whatever the sort is - except when the
             * sort IS that column: 'order by a.Id desc, a.[Id] desc' is not a tiebreak but error
             * 169, and the default sort is precisely that case.
             *
             * Asked of 'value' and not of 'field': 'value' is the sort key in the model's terms,
             * while 'field' is the SQL spelling and has three of them ('a.Id', 'a.{Name}',
             * 'r1.[Name]') - comparing that would tie this to how the string is formatted.
             *
             * Same direction as the sort: a nonclustered index is ordered by (key, clustering key),
             * so 'desc, desc' is still a backward scan while 'desc, asc' would force a Sort.
             */
            var tiebreaker = value == Constants.FieldNames.Id
                ? String.Empty
                : $", a.[{Constants.FieldNames.Id}] {dir}";
            sb.AppendLine($"order by {field} {dir}{tiebreaker}");
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

            var refMap = new RefMapBuilder(Endpoint, isPlain: false, hasDefaults: false);

            refMap.WriteRefMapIndex(sb, sx =>
            {
                sb.AppendLine();
                if (filters.Count > 0)
                {
                    foreach (var f in filters)
                        sb.Append($"insert into @map([{f.name}]) values (@{f.name});");
                    sb.AppendLine();
                }
            });

            /* Two recordsets, one word, two namespaces: the first is the row's own tags and hangs
             * off the element as a MEMBER, the second is the candidate list the filter picks from.
             * Hence the two constants - see CLAUDE.md, "Members" and "Filters".
             */
            if (Table.HasTags)
            {
                sb.AppendLine($"""
                -- tags - for elements
                select [!{TableMetadataDefaults.TagsTypeName()}!Array] = null, [Id!!Id] = t.Id,
                    [Name!!Name] = t.[Name], t.[Color], t.[Memo],
                    [!{Table.TypeName}.{Constants.FieldNames.Tags}!ParentId] = m.[Id]
                from @map m
                    inner join {TableMetadataDefaults.TagEntriesTableName(Table.Model)} e on e.[Owner] = m.[Id]
                    inner join {TableMetadataDefaults.TagsTableName()} t on t.[Id] = e.[Tag]
                where t.[For] = N'{Table.Model}';

                -- tags - for filter
                select [{Constants.FilterNames.Tags}!{TableMetadataDefaults.TagsTypeName()}!Array] = null,
                    [Id!!Id] = t.Id, [Name!!Name] = t.[Name], t.[Color], t.[Memo]
                from {TableMetadataDefaults.TagsTableName()} t where t.[For] = N'{Table.Model}'
                order by t.[Id];
                """);
            }

            // with the 'All' row: here the list is what a FILTER picks from
            foreach (var en in EnumTargets(withDetails: false))
            {
                sb.AppendLine();
                sb.AppendLine(EnumValuesRecordset(en, withAll: true));
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
            if (Table.HasPeriod)
            {
                sb.Append($", [!{collectionName}.Period.From!Filter] = @From");
                sb.Append($", [!{collectionName}.Period.To!Filter] = @To");
            }
            // always, not only when picked: the Filter has to come back the way every other one does
            if (Table.HasTags)
                sb.Append($", [!{collectionName}.{Constants.FilterNames.Tags}!Filter] "
                    + $"= @{Constants.FilterNames.Tags}");
            if (refs.Count > 0) {
                sb.Append(", ");
                /* An enum filter comes back as the bare code: that is what the ComboBox writes into
                 * the Filter and what the WHERE compares. A RefId here would resolve it through the
                 * map instead - an object of the map's type, which is not the type of the candidate
                 * list, so the control would find nothing to select.
                 *
                 */
                sb.Append(String.Join(", ", refs.Select(rt => rt.Column.IsEnum
                    ? $"[!{collectionName}.{rt.Column.Name}!Filter] = @{rt.Column.Name}"
                    : $"[!{collectionName}.{rt.Column.Name}.{rt.Table.RefTypeName}.RefId!Filter] = @{rt.Column.Name}")));
            }
            sb.AppendLine(";");
            return sb.ToString();
        }

        IEnumerable <String> XtraIndexColumns()
        {
            // the row's own tags, so the MEMBER name - the filter's candidate list is a root array
            if (Table.HasTags)
                yield return $"[{Constants.FieldNames.Tags}!{TableMetadataDefaults.TagsTypeName()}!Array] = null";
        }

        IEnumerable<String> indexSqlFields(String alias)
        {
            return Table.AllColumns(TableColumnPredicates.IsIndexColumn).Select(col => col.SqlModelColumnName(alias, t => t.RefTypeName))
                .Concat(XtraIndexColumns());
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
            if (Table.HasTags)
                dbprms.AddString($"@{Constants.FilterNames.Tags}", tags);
            var docOp = Endpoint.DocumentOperation();
            if (docOp != null)
                dbprms.AddString("@RouteOperation", docOp);
            /* The parameter is declared as what the COLUMN is, never as what a filter usually turns
             * out to be. An operation is keyed by its code; everything else by an identifier whose
             * base the database reported. Assuming bigint for both was silent in the same way: the
             * value came back null, which reads downstream as 'no filter picked' and clears the
             * selector on the way back, rather than failing anywhere visible.
             */
            foreach (var rd in refs)
            {
                var val = filters.FirstOrDefault(f => f.name == rd.Column.Name).value;
                var name = $"@{rd.Column.Name}";
                if (rd.Column.IsOperation)
                    dbprms.AddString(name, String.IsNullOrEmpty(val) ? null : val);
                /* Deliberately NOT the line above. There an empty value means the filter was not
                 * picked, so it is erased to null; here the empty string is the key of a real row -
                 * the set's 'All' - and the WHERE reads it as such. Blanking it would make the
                 * chosen 'All' indistinguishable from an untouched filter, on the way in and in the
                 * Filter property that comes back.
                 */
                else if (rd.Column.IsEnum)
                    dbprms.AddString(name, val);
                else
                    dbprms.AddTyped(name, _descr.PlatformId.SqlDbType, _descr.PlatformId.ParseId(val));
            }
        });
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Core.Extensions.Dynamic;
using A2v10.Data.Interfaces;
using DocumentFormat.OpenXml.Office2019.Excel.RichData2;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    String BuildLoadPlainSqlText()
    {
        var allColumns = Table.AllColumns().ToList();
        var refs = allColumns.AllRefs().ToList();

        IEnumerable<String> plainSqlFields(String alias)
        {
            static Boolean includeColumn(TableColumn col)
                => col.Type != ColumnType.Void;
            return Table.AllColumns(includeColumn).Select(col => col.SqlModelColumnName(alias, t => t.TypeName));
        }

        String mainDetailsFields(KeyValuePair<String, TableMetadata> detail)
        {
            var dt = detail.Value;
            if (dt.Kinds.Count == 0)
                return $"[{detail.Key}!{dt.TypeName}!Array] = null";
            else
                return String.Join(", ", dt.Kinds.Select(k => $"[{k}!{dt.TypeName}!Array] = null"));
        }

        var sb = new StringBuilder($"""
            -- load for {Table.Model}

            set nocount on;
            set transaction isolation level read uncommitted;

            """);
        sb.AppendLine();

        // STEP 1: main recordset
        sb.AppendLine("-- main recordset");

        sb.Append($"""
            select [{Table.Model}!{Table.TypeName}!Object] = null, {String.Join(", ", plainSqlFields("a"))}
            """);
        if (Table.Details.Count > 0)
        {
            sb.AppendLine(",");
            sb.Append("  ");
            sb.AppendJoin(", ", Table.Details.Select(mainDetailsFields));
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine();
        }
        sb.AppendLine($"from {Table.SqlTableName} a where a.Id = @Id;");


        if (Table.Details.Count > 0)
        {
            // STEP 2: DETAILS
            sb.AppendLine();
            foreach (var d in Table.Details)
            {
                var dt = d.Value;
                var detailsParents = dt.Kinds.Count > 0
                    ? String.Join(",\n  ", dt.Kinds.Select(k => $"[!{Table.TypeName}.{k}!ParentId] = case when d.[{dt.RowKindField}] = N'{k}' then d.[Owner] else null end"))
                    : $"[!{Table.TypeName}.{d.Key}!ParentId] = d.[Owner]";

                static Boolean includeDetailsColumn(TableColumn col)
                    => col.Type != ColumnType.RowKind && col.Type != ColumnType.Id;

                var detailsFields = d.Value.Columns.Where(c => includeDetailsColumn(c)).Select(col => col.SqlModelColumnName("d", t => t.TypeName)).ToList();
                sb.AppendLine($"""
                select [!{dt.TypeName}!Array] = null, [Id!!Id] = d.Id, [RowNo!!RowNumber] = d.RowNo,
                """);
                if (detailsFields.Count > 0)
                {
                    sb.Append($"  {String.Join(", ", detailsFields)}");
                    sb.AppendLine(",");
                }
                sb.AppendLine($"""
                  {detailsParents}
                from {dt.SqlTableName} d where d.[Owner] = @Id
                order by d.RowNo;
                """);
            }
        }


        var refMap = new RefMapBuilder(Table, isPlain: true);

        // STEP 3: map recordsets

        refMap.WriteRefMap(sb);

        // STEP 5: system recorset
        if (Table.IsDocument)
        {
            sb.AppendLine();
            sb.AppendLine("-- system recordset");
            sb.Append($"""
                select [!$System!] = null, [!!ReadOnly] = a.Done
                from {Table.SqlTableName} a where a.Id = @Id;
                """);
        }
        return sb.ToString();
    }


    public async Task<IDataModel> LoadPlainModelAsync()
    {

        var sqlQuery = BuildLoadPlainSqlText();

        return await _dbContext.LoadModelSqlAsync(_descr.DataSource, sqlQuery, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddString("@Id", _descr.PlatformUrl.Id);
        });
    }

    public async Task<ExpandoObject> SavePlainModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {
        String CheckRowVersion()
        {
            if (Table.AllColumns(c => c.Type == ColumnType.RowVersion).Any())
            {
                var elemName = Table.IsDocument ? "Document" : "Element";
                return $"""
                    if exists(select * from @{Table.Model} t inner join {Table.SqlTableName} c on c.Id = t.Id
                        where t.rv is not null and t.rv <> c.rv)
                    throw 60000, N'UI:@[Error.{elemName}.RowVersion]', 0;
                """;
            }
            return String.Empty;
        }

        String MergeDetails()
        {
            if (Table.Details == null || Table.Details.Count == 0)
                return String.Empty;
            var sb = new StringBuilder("-- merge details");
            sb.AppendLine();

            Boolean updateablePredicate(TableColumn c)
                => c.Type != ColumnType.Owner && c.Type != ColumnType.RowKind && c.Type != ColumnType.Id;

            String mergeOneDetails(TableMetadata detailsTable)
            {
                var updateFields = detailsTable.AllColumns(updateablePredicate);

                return $"""
				merge {detailsTable.SqlTableName} as t
				using @{detailsTable.Table} as s
				on t.[Id]  = s.[Id]
				when matched then update set
				    {String.Join(',', updateFields.Select(f => $"t.[{f.Name}] = s.[{f.Name}]"))}
				when not matched then insert 
				    ([Owner], {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
				    (@Id, {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				when not matched by source and t.[Owner] = @Id then delete;
				""";
            }

            String mergeMultiDetails(TableMetadata detailsTable)
            {
                var updateFields = detailsTable.AllColumns(updateablePredicate);
                var kindField = detailsTable.Columns.FirstOrDefault(c => c.Type == ColumnType.RowKind)
                    ?? throw new InvalidOperationException("Kind field not found");

                var usingDetails = detailsTable.Kinds.Select(k =>
                    $"select [__Kind__] = N'{k}', * from @{k}"
                );

                return $"""
				with ST as (
				    {String.Join("\nunion all\n", usingDetails)}
				)
				merge {detailsTable.SqlTableName} as t
				using ST as s
				on t.Id = s.Id
				when matched then update set
					{String.Join(',', updateFields.Select(f => $"t.[{f.Name}] = s.[{f.Name}]"))}
				when not matched then insert 
					([Owner], [{kindField.Name}], {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
					(@Id, s.[__Kind__], {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				when not matched by source and t.[Owner] = @Id then delete;
				""";
            }

            foreach (var details in Table.Details.Select(x => x.Value))
            {
                if (details.Kinds.Count == 0)
                    sb.AppendLine(mergeOneDetails(details));
                else
                    sb.AppendLine(mergeMultiDetails(details));
                sb.AppendLine();
            }
            return sb.ToString();
        }


        String buildSqlUpdateText()
        {

            var updatedFields = Table.AllColumns(c => c.IsFieldUpdated()).Select(c => $"t.[{c.Name}] = s.[{c.Name}]");
            var insertedFields = Table.AllColumns(c => c.IsFieldUpdated()).Select(c => $"[{c.Name}]");

            var sb = new StringBuilder("""
            set nocount on;
            set transaction isolation level read committed;
            set xact_abort on;
            
            declare @rtable table(Id platformid);
            declare @Id platformid;                        

            """);
            // STEP:1 - check row version
            sb.AppendLine(CheckRowVersion());

            // STEP:2 - merge main
            sb.AppendLine($"""
            -- merge main table
            merge {Table.SqlTableName} as t
            using @{Table.Model} as s
            on t.[Id] = s.[Id]
            when matched then update set
              {String.Join(",\n", updatedFields)}
            when not matched then insert
              ({String.Join(',', insertedFields)}) values
              ({String.Join(',', insertedFields)}) 
            output inserted.[Id] into @rtable([Id]);
            
            select @Id = [Id] from @rtable;
            
            """);

            // STEP:3 update details

            sb.AppendLine(MergeDetails());

            // STEP:4 return select

            sb.AppendLine(BuildLoadPlainSqlText());

            return sb.ToString();
        }

        var sqlText = buildSqlUpdateText();

        var item = data.Get<ExpandoObject>(Table.Model);
        var tableBuilder = new DataTableBuilder(Table);
        var dtable = tableBuilder.BuildDataTable(item);

        List<(String name, String typeName, DataTable table)> detailsTables = [];

        if (Table.Details.Count > 0)
        {
            foreach (var t in Table.Details)
            {
                var detailsTableBuilder = new DataTableBuilder(t.Value);
                if (t.Value.Kinds.Count > 0)
                {
                    foreach (var k in t.Value.Kinds)
                    {
                        var rows = item?.Get<List<Object>>($"{k}");
                        var dt = detailsTableBuilder.BuildDataTable(rows);
                        detailsTables.Add(($"@{k}", t.Value.SqlTableTypeName, dt));
                    }
                }
            }
        }

        var dm = await _dbContext.LoadModelSqlAsync(DataSource, sqlText, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddStructured($"@{Table.Model}", Table.SqlTableTypeName, dtable);
            foreach (var d in detailsTables)
                dbprms.AddStructured(d.name, d.typeName, d.table);
        });

        return dm.Root;
    }
}

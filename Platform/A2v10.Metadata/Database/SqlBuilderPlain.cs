// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Core.Extensions.Dynamic;
using A2v10.Data.Interfaces;

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
        sb.AppendLine();
        sb.AppendLine("-- system recordset");
        sb.Append($"""
            select [!$System!] = null;
            """);
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

        String buildSqlUpdateText()
        {

            var updatedFields = Table.AllColumns(c => c.IsFieldUpdated()).Select(c => $"t.[{c.Name}] = s.[{c.Name}]");
            var insertedFields = Table.AllColumns(c => c.IsFieldUpdated()).Select(c => $"[{c.Name}]");

            var sb = new StringBuilder("""
            set nocount on;
            set transaction isolation level read committed;
            set xact_abort on;
            
            declare @rtable table(Id bigint);
            declare @Id bigint;                        

            """);

            // STEP:1 - merge main
            sb.AppendLine($"""
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

            // STEP:3 return select

            sb.AppendLine(BuildLoadPlainSqlText());

            return sb.ToString();
        }

        var sqlText = buildSqlUpdateText();

        var item = data.Get<ExpandoObject>(Table.Model);
        var tableBuilder = new DataTableBuilder(Table);
        var dtable = tableBuilder.BuildDataTable(item);

        var dm = await _dbContext.LoadModelSqlAsync(DataSource, sqlText, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddStructured($"@{Table.Model}", Table.SqlTableTypeName, dtable);

        });

        return dm.Root;
    }
}

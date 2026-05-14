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

        var sb = new StringBuilder($"""
            -- load for {Table.Model}

            set nocount on;
            set transaction isolation level read uncommitted;
            """);
        sb.AppendLine();

        // STEP 1: main recordset
        sb.AppendLine("-- main recordset");

        sb.AppendLine($"""
            select [{Table.Model}!{Table.TypeName}!Object] = null, {String.Join(", ", plainSqlFields("a"))}
            from {Table.SqlTableName} a where a.Id = @Id;
            """);

        // STEP 5: map recordsets
        if (refs.Count > 0)
        {
            // STEP 2: temp table
            sb.AppendLine();
            sb.AppendLine("-- map table");
            sb.AppendLine($"declare @map table({String.Join(", ", refs.Select(c => $"[{c.Column.Name}] bigint"))});");
            sb.AppendLine($"""
                insert into @map({String.Join(", ", refs.Select(c => $"[{c.Column.Name}]"))})
                select {String.Join(", ", refs.Select(c => $"[{c.Column.Name}]"))}
                from {Table.SqlTableName} a where a.Id = @Id;
                """);
            sb.AppendLine();
            sb.AppendLine("-- map recordsets");
            var groupTables = refs.GroupBy(x => x.Table.Table).ToList();
            foreach (var gt in groupTables)
            {
                var gx = gt.First();
                var inClause = String.Join(", ", gt.Select(x => $"t.[{x.Column.Name}]"));
                sb.AppendLine($"""
                    select [!{gx.Table.TypeName}!Map] = null, [Id!!Id] = r.Id, [{gx.Column.Presentation}!!Name] = r.[{gx.Column.Presentation}]
                    from {gx.Table.SqlTableName} r inner join @map t on r.Id in ({inClause});
                    """);
                sb.AppendLine();
            }
        }

        // STEP 5: system recorset (filters -> always!)
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
            dbprms.AddStructured($"@{Table.Model}", Table.TableTypeName, dtable);

        });

        return dm.Root;
    }
}

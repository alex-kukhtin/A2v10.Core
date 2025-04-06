// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    private String LoadPlainModelSql(TableMetadata table, AppMetadata appMeta)
    {
        String DetailsArray()
        {
            if (!table.Details.Any())
                return String.Empty;
            var detailsArray = table.Details.Select(t => $"[{t.RealItemsName}!{t.RealTypeName}!Array] = null");
            return $",\n{String.Join(',', detailsArray)}";
        }

        String DetailsContent()
        {
            if (!table.Details.Any())
                return String.Empty;
            var sb = new StringBuilder();

            foreach (var t in table.Details)
            {
                sb.AppendLine($"""
                select [!{t.RealTypeName}!Array] = null,
                    [!{table.RealTypeName}.{t.RealItemsName}!ParentId] = d.[{appMeta.ParentField}],
                    {String.Join(",", t.AllSqlFields("d", appMeta, isDetails:true))}
                from {t.Schema}.[{t.Name}] d
                    {RefTableJoins(t.RefFields(), "d")}
                where d.[{appMeta.ParentField}] = @Id
                order by d.[{appMeta.RowNoField}];
                
                """);

            }
            return sb.ToString();
        }

        String SystemRecordset()
        {
            if (table.IsDocument)
                return $"""
				select [!$System!] = null, [!!ReadOnly] = d.Done
				from {table.SqlTableName} d where Id = @Id;
				""";
            return String.Empty;
        }

        return $"""
        
        select [{table.RealItemName}!{table.RealTypeName}!Object] = null,
            {String.Join(",", table.AllSqlFields("a", appMeta))}{DetailsArray()}
        from 
        {table.Schema}.[{table.Name}] a
            {RefTableJoins(table.RefFields(), "a")}
        where a.[Id] = @Id;

        {DetailsContent()}

        {SystemRecordset()}

        """;

    }
    public Task<IDataModel> LoadPlainModelAsync()
    {

        var sqlString = $"""
            set nocount on;
            set transaction isolation level read uncommitted;
            
            {LoadPlainModelSql(_table, _appMeta)};
            
            """;

        return _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddString("@Id", _platformUrl.Id);
        });
    }
    public async Task<ExpandoObject> SavePlainModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {

        var updatedFields = _table.Columns.Where(c => c.IsFieldUpdated(_appMeta)).Select(c => $"t.[{c.Name}] = s.[{c.Name}]");

        var insertedFields = _table.Columns.Where(c => c.IsFieldUpdated(_appMeta)).Select(c => $"[{c.Name}]");

        String onPrimaryKeys()
        {
            return String.Join(" and ", _table.PrimaryKeys.Select(c => $"t.[{c.Name}]  = s.[{c.Name}]"));
        }

        String MergeDetails()
        {
            if (_table.Details == null || _table.Details.Count == 0)
                return String.Empty;
            var sb = new StringBuilder();
            foreach (var details in _table.Details)
            {
                var updateFields = details.Columns.Where(f => !f.IsParent && f.Name != _appMeta.IdField);
                var parentField = details.Columns.FirstOrDefault(f => f.IsParent)
                    ?? throw new InvalidOperationException("Parent field not found");
                sb.AppendLine($"""
				merge {details.SqlTableName} as t
				using @{details.Name} as s
				on t.Id = s.Id
				when matched then update set
					{String.Join(',', updateFields.Select(f => $"t.[{f.Name}] = s.[{f.Name}]"))}
				when not matched then insert 
					({parentField.Name}, {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
					(@Id, {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				when not matched by source and t.[{parentField.Name}] = @Id then delete;
				""");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        var idDataTypeString = _table.PrimaryKeys.First().SqlDataType(_appMeta.IdDataType);

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;
        
        declare @rtable table(Id {idDataTypeString});
        declare @Id {idDataTypeString};
        
        merge {_table.SqlTableName} as t
        using @{_table.RealItemName} as s
        on {onPrimaryKeys()}
        when matched then update set
        {String.Join(",\n", updatedFields)}
        when not matched then insert
        ({String.Join(',', insertedFields)}) values
        ({String.Join(',', insertedFields)}) 
        output inserted.Id into @rtable(Id);

        select @Id = Id from @rtable;
        
        {MergeDetails()}

        {LoadPlainModelSql(_table, _appMeta)}
        """;
        var item = data.Get<ExpandoObject>(_table.RealItemName);
        var tableBuilder = new DataTableBuilder(_table, _appMeta);
        
        var dtable = tableBuilder.BuildDataTable(item);

        var dm = await _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddStructured($"@{_table.RealItemName}", _table.TableTypeName, dtable);
            _table.Details.ForEach(details =>
            {
                var rows = item?.Get<List<Object>>($"{details.Name}");
                var detailsTableBuilder = new DataTableBuilder(details, _appMeta);
                var detailsTable = detailsTableBuilder.BuildDataTable(rows);
                dbprms.AddStructured($"@{details.Name}", details.TableTypeName, detailsTable);
            });
        });
        return dm.Root; 
    }

    String DumpDataTable(DataTable dataTable)
    {
        var sb = new StringBuilder();
        foreach (DataRow row in dataTable.Rows)
        {
            foreach (DataColumn col in dataTable.Columns)
            {
                sb.AppendLine($"{col.ColumnName} [{col.DataType}] = {row[col]} ");
            }
            sb.AppendLine("-----------------");
        }
        return sb.ToString();
    }
}

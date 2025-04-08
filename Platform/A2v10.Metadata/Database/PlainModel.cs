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
    private async Task<String> LoadPlainModelSqlAsync(TableMetadata table)
    {
        String DetailsArray()
        {
            if (!table.Details.Any())
                return String.Empty;
            var detailsArray = table.Details.Select(t => $"[{t.RealItemsName}!{t.RealTypeName}!Array] = null");
            return $",\n{String.Join(',', detailsArray)}";
        }

        async Task<String> DetailsContentAsync()
        {
            if (!table.Details.Any())
                return String.Empty;
            var sb = new StringBuilder();

            foreach (var t in table.Details)
            {
                var refFields = await ReferenceFieldsAsync(t);  

                var parentField = refFields.FirstOrDefault(r => r.Table.SqlTableName == _table.SqlTableName)
                    ?? throw new InvalidOperationException($"Parent field not found in {t.SqlTableName}");

                sb.AppendLine($"""
                select [!{t.RealTypeName}!Array] = null,
                    [!{table.RealTypeName}.{t.RealItemsName}!ParentId] = d.[{parentField.Column.Name}],
                    {String.Join(",", t.AllSqlFields(refFields, "d", isDetails:true))}
                from {t.Schema}.[{t.Name}] d
                    {RefTableJoins(refFields, "d")}
                where d.[{parentField.Column.Name}] = @Id
                order by d.[{t.RowNoField}];
                
                """);

            }
            return sb.ToString();
        }

        String SystemRecordset()
        {
            if (table.IsDocument)
                return $"""
				select [!$System!] = null, [!!ReadOnly] = d.{_table.DoneField}
				from {table.SqlTableName} d where Id = @Id;
				""";
            return String.Empty;
        }

        var tableRefFields = await ReferenceFieldsAsync(table);
        return $"""
        
        select [{table.RealItemName}!{table.RealTypeName}!Object] = null,
            {String.Join(",", table.AllSqlFields(tableRefFields, "a"))}{DetailsArray()}
        from 
        {table.Schema}.[{table.Name}] a
            {RefTableJoins(tableRefFields, "a")}
        where a.[Id] = @Id;

        {await DetailsContentAsync()}

        {SystemRecordset()}

        """;

    }
    public async Task<IDataModel> LoadPlainModelAsync()
    {

        var sqlString = $"""
            set nocount on;
            set transaction isolation level read uncommitted;
            
            {await LoadPlainModelSqlAsync(_table)};
            
            """;

        return await _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddString("@Id", _platformUrl.Id);
        });
    }
    public async Task<ExpandoObject> SavePlainModelAsync(ExpandoObject data, ExpandoObject savePrms)
    {

        var updatedFields = _table.Columns.Where(c => c.IsFieldUpdated()).Select(c => $"t.[{c.Name}] = s.[{c.Name}]");

        var insertedFields = _table.Columns.Where(c => c.IsFieldUpdated()).Select(c => $"[{c.Name}]");

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
                var updateFields = details.Columns.Where(f => !f.Role.HasFlag(TableColumnRole.Parent) && !f.Role.HasFlag(TableColumnRole.PrimaryKey));
                var parentField = details.Columns.FirstOrDefault(f => f.Role.HasFlag(TableColumnRole.Parent))
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

        {await LoadPlainModelSqlAsync(_table)}
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

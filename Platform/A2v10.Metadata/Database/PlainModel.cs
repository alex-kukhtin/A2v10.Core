// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Data.SqlClient;

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

        return $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        
        select [{table.RealItemName}!{table.RealTypeName}!Object] = null,
            {String.Join(",", table.AllSqlFields("a", appMeta))}{DetailsArray()}
        from 
        {table.Schema}.[{table.Name}] a
            {RefTableJoins(table.RefFields(), "a")}
        where a.[Id] = @Id;

        {DetailsContent()}

        """;

    }
    public Task<IDataModel> LoadPlainModelAsync()
    {

        var sqlString = LoadPlainModelSql(_table, _appMeta);

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
				merge {details.SqlTableName()} as t
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

        // todo: Id from
        var sqlString = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;
        
        declare @rtable table(Id {_appMeta.IdDataType});
        declare @Id {_appMeta.IdDataType};
        
        merge {_table.SqlTableName()} as t
        using @{_table.RealItemName} as s
        on t.[{_appMeta.IdField}] = s.[{_appMeta.IdField}]
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
            dbprms.Add(new SqlParameter($"@{_table.RealItemName}", SqlDbType.Structured) { TypeName = _table.TableTypeName, Value = dtable });
            _table.Details.ForEach(details =>
            {
                var rows = item?.Get<List<Object>>($"{details.Name}");
                var detailsTableBuilder = new DataTableBuilder(details, _appMeta);
                var detailsTable = detailsTableBuilder.BuildDataTable(rows);
                dbprms.Add(new SqlParameter($"@{details.Name}", SqlDbType.Structured) { TypeName = details.TableTypeName, Value = detailsTable });
            });
        });
        return dm.Root; 
    }
}

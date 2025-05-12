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

            var detailsArray = table.Details.Select(d => {
                if (d.Kinds.Count == 0)
                    return $"[{d.RealItemsName}!{d.RealTypeName}!Array] = null";
                return String.Join(",", d.Kinds.Select(k => $"[{k.Name}!{d.RealTypeName}!Array] = null"));
            });
            return $",\n{String.Join(',', detailsArray)}";
        }

        async Task<String> DetailsContentAsync()
        {
            if (!table.Details.Any())
                return String.Empty;
            var sb = new StringBuilder();

            foreach (var t in table.Details)
            {

                //TODO: Use: Same Key

                var refFields = await ReferenceFieldsAsync(t);

                String? kindField = t.Kinds.Count > 0 ? t.KindField : null;

                var parentField = refFields.FirstOrDefault(r => r.Table.SqlTableName == _table.SqlTableName)?.Column.Name
                    ?? t.PrimaryKeyField;


                var detailsParentIdField = $"[!{table.RealTypeName}.{t.RealItemsName}!ParentId] = d.[{parentField}]";

                if (t.Kinds.Count > 0)
                {
                    detailsParentIdField = String.Join(',', t.Kinds.Select(k =>
                        $"[!{table.RealTypeName}.{k.Name}!ParentId] = case when d.[{kindField}] = N'{k.Name}' then d.[{parentField}] else null end"
                    ));
                }

                sb.AppendLine($"""
                select [!{t.RealTypeName}!Array] = null,
                    {detailsParentIdField},
                    {String.Join(",", t.AllSqlFields(refFields, "d", isDetails:true))}
                from {t.Schema}.[{t.Name}] d
                {RefTableJoins(refFields, "d")}
                where d.[{parentField}] = @Id
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
				from {table.SqlTableName} d where [{table.PrimaryKeyField}] = @Id;
				""";
            return String.Empty;
        }

        var tableRefFields = await ReferenceFieldsAsync(table);

        return $"""
        
        select [{table.RealItemName}!{table.RealTypeName}!Object] = null,
            {String.Join(",", table.AllSqlFields(tableRefFields, "a"))}{DetailsArray()}
        from {table.Schema}.[{table.Name}] a
        {RefTableJoins(tableRefFields, "a")}
        where a.[{table.PrimaryKeyField}] = @Id;

        {await DetailsContentAsync()}

        {EnumsMapSql(tableRefFields, false)}

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

            String mergeOneDetails(TableMetadata detailsTable)
            {
                var updateFields = detailsTable.Columns.Where(f => !f.Role.HasFlag(TableColumnRole.Parent) && !f.Role.HasFlag(TableColumnRole.PrimaryKey) && !f.Role.HasFlag(TableColumnRole.Kind));

                var multPk = detailsTable.PrimaryKeys.Count() > 1;

                if (multPk)
                {
                    var parentField = detailsTable.PrimaryKeys.FirstOrDefault(pk => !pk.Role.HasFlag(TableColumnRole.RowNo))
                        ?? throw new InvalidOperationException("MergeDetails: Primary key not found");
                    var rowNoField = detailsTable.PrimaryKeys.FirstOrDefault(pk => pk.Role.HasFlag(TableColumnRole.RowNo))
                        ?? throw new InvalidOperationException("MergeDetails: RowNo field not found");
                    return $"""
				    merge {detailsTable.SqlTableName} as t
				    using @{detailsTable.Name} as s
				    on {String.Join(" and ", detailsTable.PrimaryKeys.Select(c => $"t.[{c.Name}] = s.[{c.Name}]"))}
				    when matched then update set
				        {String.Join(',', updateFields.Select(f => $"t.[{f.Name}] = s.[{f.Name}]"))}
				    when not matched then insert 
				        ([{parentField.Name}], [{rowNoField.Name}], {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
				        (@Id, s.[{rowNoField.Name}], {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				    when not matched by source and t.[{parentField.Name}] = @Id then delete;
				    """;
                }
                else
                {
                    var parentField = detailsTable.Columns.FirstOrDefault(f => f.Role.HasFlag(TableColumnRole.Parent))
                        ?? throw new InvalidOperationException("MergeDetails: Parent field not found");
                    return $"""
				    merge {detailsTable.SqlTableName} as t
				    using @{detailsTable.Name} as s
				    on t.[{detailsTable.PrimaryKeyField}]  = s.[{detailsTable.PrimaryKeyField}]
				    when matched then update set
				        {String.Join(',', updateFields.Select(f => $"t.[{f.Name}] = s.[{f.Name}]"))}
				    when not matched then insert 
				        ([{parentField.Name}], {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
				        (@Id, {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				    when not matched by source and t.[{parentField.Name}] = @Id then delete;
				    """;
                }
            }

            String mergeMultiDetails(TableMetadata detailsTable)
            {
                var updateFields = 
                    detailsTable.Columns.Where(f => !f.Role.HasFlag(TableColumnRole.Parent) && !f.Role.HasFlag(TableColumnRole.PrimaryKey) && !f.Role.HasFlag(TableColumnRole.Kind));
                var parentField = detailsTable.Columns.FirstOrDefault(f => f.Role.HasFlag(TableColumnRole.Parent))
                    ?? throw new InvalidOperationException("Parent field not found");
                var kindField = detailsTable.Columns.FirstOrDefault(f => f.Role.HasFlag(TableColumnRole.Kind))
                    ?? throw new InvalidOperationException("Kind field not found");

                var usingDetails = detailsTable.Kinds.Select(k => 
                    $"select [__Kind__] = N'{k.Name}', * from @{k.Name}"
                );

                return $"""
				with ST as (
				    {String.Join("\nunion all\n", usingDetails)}
				)
				merge {detailsTable.SqlTableName} as t
				using ST as s
				on t.{detailsTable.PrimaryKeyField} = s.{detailsTable.PrimaryKeyField}
				when matched then update set
					{String.Join(',', updateFields.Select(f => $"t.[{f.Name}] = s.[{f.Name}]"))}
				when not matched then insert 
					([{parentField.Name}], [{kindField.Name}], {String.Join(',', updateFields.Select(f => $"[{f.Name}]"))}) values
					(@Id, s.[__Kind__], {String.Join(',', updateFields.Select(f => $"s.[{f.Name}]"))})
				when not matched by source and t.[{parentField.Name}] = @Id then delete;
				""";
            }

            foreach (var details in _table.Details)
            {
                if (details.Kinds.Count == 0)
                    sb.AppendLine(mergeOneDetails(details));
                else
                    sb.AppendLine(mergeMultiDetails(details));
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
        output inserted.[{_table.PrimaryKeyField}] into @rtable([Id]);

        select @Id = [Id] from @rtable;
        
        {MergeDetails()}

        {await LoadPlainModelSqlAsync(_table)}
        """;
        var item = data.Get<ExpandoObject>(_table.RealItemName);
        var tableBuilder = new DataTableBuilder(_table, _appMeta);
        
        var dtable = tableBuilder.BuildDataTable(item);

        //var str = DumpDataTable(dtable);

        var dm = await _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddStructured($"@{_table.RealItemName}", _table.TableTypeName, dtable);
            _table.Details.ForEach(details =>
            {
                var detailsTableBuilder = new DataTableBuilder(details, _appMeta);
                if (details.Kinds.Count > 0)
                {
                    foreach (var k in details.Kinds)
                    {
                        var rows = item?.Get<List<Object>>($"{k.Name}");
                        var detailsTable = detailsTableBuilder.BuildDataTable(rows);
                        dbprms.AddStructured($"@{k.Name}", details.TableTypeName, detailsTable);
                    }
                }
                else
                {
                    var rows = item?.Get<List<Object>>($"{details.RealItemsName}");
                    var detailsTable = detailsTableBuilder.BuildDataTable(rows);
                    dbprms.AddStructured($"@{details.Name}", details.TableTypeName, detailsTable);
                }
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

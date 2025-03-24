// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using Microsoft.Data.SqlClient;

namespace A2v10.Metadata;

internal partial class DatabaseModelProcessor
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
                    {RefTableJoins(t.RefFields(), "d", appMeta)}
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
            {RefTableJoins(table.RefFields(), "a", appMeta)}
        where a.[Id] = @Id;

        {DetailsContent()}
        -- SELECT ALL DETAILS HERE

        """;

    }
    public Task<IDataModel> LoadPlainModelAsync(TableMetadata table, IPlatformUrl platformUrl, IModelView view, AppMetadata appMeta)
    {
        var viewMeta = view.Meta ??
           throw new InvalidOperationException($"view.Meta is null");

        var sqlString = LoadPlainModelSql(table, appMeta);

        return _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddString("@Id", platformUrl.Id);
        });
    }

    public async Task<ExpandoObject> SaveModelAsync(IModelView view, ExpandoObject data, ExpandoObject savePrms)
    {
        if (view.Meta == null)
            throw new InvalidOperationException("Meta is null");
        var table = await _metadataProvider.GetSchemaAsync(view.Meta, view.DataSource);
        var appMeta = await _metadataProvider.GetAppMetadataAsync(view.DataSource);

        var updatedFields = table.Columns.Where(c => c.IsFieldUpdated(appMeta)).Select(c => $"t.[{c.Name}] = s.[{c.Name}]");

        var insertedFields = table.Columns.Where(c => c.IsFieldUpdated(appMeta)).Select(c => $"[{c.Name}]");

        // todo: Id from
        var sqlString = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;
        
        declare @rtable table(Id {appMeta.IdDataType});
        declare @Id {appMeta.IdDataType};
        
        merge {table.Schema}.[{table.Name}] as t
        using @{table.RealItemName} as s
        on t.[{appMeta.IdField}] = s.[{appMeta.IdField}]
        when matched then update set
        {String.Join(",\n", updatedFields)}
        when not matched then insert
        ({String.Join(',', insertedFields)}) values
        ({String.Join(',', insertedFields)}) 
        output inserted.Id into @rtable(Id);

        select @Id = Id from @rtable;

        {LoadPlainModelSql(table, appMeta)}
        """;
        var item = data.Get<ExpandoObject>(table.RealItemName);
        var tableBuilder = new DataTableBuilder(table, appMeta);
        var dtable = tableBuilder.BuildDataTable(item);

        var dm = await _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.Add(new SqlParameter($"@{table.RealItemName}", SqlDbType.Structured) { TypeName = table.TableTypeName, Value = dtable });
        });
        return dm.Root; 
    }
}

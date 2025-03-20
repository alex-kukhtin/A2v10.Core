// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class DatabaseModelProcessor
{
    public Task<IDataModel> LoadPlainModelAsync(TableMetadata table, IPlatformUrl platformUrl, IModelView view, AppMetadata appMeta)
    {
        var viewMeta = view.Meta ??
           throw new InvalidOperationException($"view.Meta is null");

        var refFields = table.RefFields();

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        
        select [{table.Name.Singular()}!{table.ModelType}!Object] = null,
            {String.Join(",", table.AllSqlFields("a", appMeta))},
            [!!RowCount]  = count(*) over()        
        from {table.Schema}.[{table.Name}] a
            {RefTableJoins(refFields, appMeta)}
        where a.[Id] = @Id
        """;

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

        when not matched then insert
        () values
        () 
        output inserted.Id into @rtable(Id);

        select @Id = Id from @rtable;


        """;
        var dm = await _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
        });
        return dm.Root; 
    }
}

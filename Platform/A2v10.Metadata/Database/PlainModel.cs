// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
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
}

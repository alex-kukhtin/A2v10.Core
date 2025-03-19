// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

internal partial class DatabaseModelProcessor
{
    public Task<IDataModel> LoadPlainModelAsync(TableMetadata meta, IPlatformUrl platformUrl, IModelView view)
    {
        var viewMeta = view.Meta ??
           throw new InvalidOperationException($"view.Meta is null");

        var refFields = meta.RefFields();

        var sqlString = $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        
        select [{meta.Name.Singular()}!{meta.ModelType}!Object] = null,
            {String.Join(",", meta.SelectFieldsAll("a", refFields))},
            [!!RowCount]  = count(*) over()        
        from {meta.SqlTableName} a
            {RefTableJoins(refFields)}
        where a.[Id] = @Id
        """;

        return _dbContext.LoadModelSqlAsync(view.DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddString("@Id", platformUrl.Id);
        });
    }
}

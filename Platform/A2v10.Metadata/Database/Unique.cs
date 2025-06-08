// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;
using System.Linq;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

internal partial class PlainModelBuilder
{
    internal async Task<IInvokeResult> CheckUniqueAsync(ExpandoObject? prms, String property)
    {
        var column = _table.Columns.FirstOrDefault(c => c.Name.Equals(property, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Column {property} not found in table {_table.Name}");

        var sql = $"""
        set nocount on;
        set transaction isolation level read uncommitted;

        declare @valid bit = 1;

        if exists(select 1 from {_table.SqlTableName} t where t.[{column.Name}] = @Value and t.Id <> @Id)
            set @valid = 0;

        select [Result!TResult!Object] = null, [Value] = @valid;
        """;

        var model = await _dbContext.LoadModelSqlAsync(_dataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddBigInt("@Id", prms?.Get<Int64>("Id"))
            .AddString("@Value", prms?.Get<String>("Value"));
        });
        return model.ToInvokeResult();
    }
}

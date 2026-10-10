// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Dynamic;
using System.Linq;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    internal async Task<IInvokeResult> CheckUniqueAsync(ExpandoObject? prms, String property)
    {
        var column = Table.AllColumns().FirstOrDefault(c => c.Name.Equals(property, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Column {property} not found in table {Table.Table}");

        /* A new record has no id, and 'Id <> null' is unknown - so the null is said out loud, or every
         * value of a new record is unique. Over a shared table, the rows of this document alone: another
         * document's number is not a duplicate of this one's. A voided row keeps its code and blocks it,
         * as the unique index to come will (ISSUES 3.9).
         */
        var own = OwnOperations("t") is { } ownRows ? $" and {ownRows}" : String.Empty;
        var sql = $"""
        set nocount on;
        set transaction isolation level read uncommitted;
        {GateSql(Gate.View)}

        declare @valid bit = 1;

        if exists(select 1 from {Table.SqlTableName} t where t.[{column.Name}] = @Value and (@Id is null or t.Id <> @Id){own})
            set @valid = 0;

        select [Result!TResult!Object] = null, [Value] = @valid;
        """;

        var model = await _dbContext.LoadModelSqlAsync(DataSource, sql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddTyped("@Id", PlatformId.SqlDbType, PlatformId.ParseId(prms?.Get<Object>("Id")?.ToString()))
            .AddString("@Value", prms?.Get<String>("Value"));
        });
        return model.ToInvokeResult();
    }
}

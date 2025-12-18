// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Dynamic;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Core.Extensions.Dynamic;

namespace A2v10.Metadata;

internal partial class IndexModelBuilder
{
    public Task DbRemoveAsync(String? propName, ExpandoObject execPrms)
    {
        var rf = _table.RefsToMe; // таблиці де є reference
        var sqlString = """
            set nocount on;
            set transaction isolation level read committed;
            set xact_abort on;            
            """;
        if (_table.IsDocument)
        {
            sqlString += $"""

            declare @Done bit;
            select @Done = Done from {_table.SqlTableName} where Id = @Id;
            if @Done = 1
                throw 600000, N'@[Error.Document.AlreadyApplied]', 0;
            else
                update {_table.SqlTableName} set [Void] = 1 where [Id] = @Id;          
            """;
        }
        else
        {
            sqlString += $"""

            // TODO: check using
            update {_table.SqlTableName} set [Void] = 1 where [Id] = @Id;
            """;
        }
        return _dbContext.LoadModelSqlAsync(_dataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddTyped("@Id", _appMeta.IdDataType.ToSqlDbType(_appMeta.IdDataType), execPrms.Get<Object>("Id"));
        });
    }
}

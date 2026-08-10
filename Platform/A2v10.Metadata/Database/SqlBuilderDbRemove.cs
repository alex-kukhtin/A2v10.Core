// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Core.Extensions.Dynamic;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    public async Task DbRemoveAsync(String? propName, ExpandoObject execPrms)
    {
        var refsToMe = await _metadataProvider.GetTableReferrersAsync(DataSource, Table);
        var rf = refsToMe.GroupBy(x => x.SqlTableName).ToList(); // tables holding a reference to this one
        var checkSql = "";
        if (rf.Count > 0)
        {
            var existsRef = rf.Select(tr => {
                var columnList = String.Join(", ", tr.Select(x => $"[{x.Column}]"));
                return $"exists (select 1 from {tr.Key} where Void = 0 and @Id in ({columnList}))";
            });

            checkSql = $"""
            if {String.Join(" or\n", existsRef)}
                throw 60000, N'UI:@[Error.Delete.Used]', 0;
            """;
        }

        var sqlString = $"""
            set nocount on;
            set transaction isolation level read committed;
            set xact_abort on;            

            {checkSql}
            """;
        if (Table.IsDocument)
        {
            sqlString += $"""
            
            declare @Done bit;
            select @Done = Done from {Table.SqlTableName} where Id = @Id;
            if @Done = 1
                throw 600000, N'@[Error.Document.AlreadyApplied]', 0;
            else
                update {Table.SqlTableName} set [Void] = 1 where [Id] = @Id;          
            """;
        }
        else
        {
            sqlString += $"""

            update {Table.SqlTableName} set [Void] = 1 where [Id] = @Id;
            """;
        }
        await _dbContext.LoadModelSqlAsync(DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            // TODO: platformid
            dbprms.AddTyped("@Id", SqlDbType.BigInt, execPrms.Get<Object>("Id"));
        });
    }
}

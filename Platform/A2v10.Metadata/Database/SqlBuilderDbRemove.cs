// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
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
            // Void is asked of the RECORD, and for a table that is part of another one the record
            // is the master: a details row has no Void of its own and never had one
            var existsRef = rf.Select(tr => {
                var first = tr.First();
                var columnList = String.Join(", ", tr.Select(x => $"r.[{x.Column}]"));
                var master = first.MasterSqlTableName;
                var join = master == null ? String.Empty
                    : $"{Environment.NewLine}        inner join {master} m"
                        + $" on m.[{Constants.FieldNames.Id}] = r.[{first.MasterColumn}]";
                var voidFrom = master == null ? "r" : "m";
                return $"exists (select 1 from {tr.Key} r{join}{Environment.NewLine}"
                    + $"        where {voidFrom}.[{Constants.FieldNames.Void}] = 0"
                    + $" and @Id in ({columnList}))";
            });

            checkSql = $"""
            if {String.Join(" or\n", existsRef)}
                {SqlExtensions.UserError("Error.Delete.Used")}
            """;
        }

        var sqlString = $"""
            set nocount on;
            set transaction isolation level read committed;
            set xact_abort on;
            {GateSql(Gate.Delete)}{RecordCheck("@Id")}
            {checkSql}
            """;
        /* What the save refuses to rewrite, the delete refuses to void: one flag (ReadOnlyFlag) - a posted
         * document, a row the deploy writes. The system account was voided by a direct dbRemove while its
         * save was refused, and only a deploy that moved the hash brought it back. Claimed in the statement
         * that writes Void, as the post claims Done (SqlBuilderPost).
         */
        if (ReadOnlyFlag("a") is { } flag)
            sqlString += $"""

            update a set [Void] = 1{ModifiedStamp()} from {Table.SqlTableName} a where a.[Id] = @Id and {flag} = 0;
            if @@rowcount = 0
                {ReadOnlyError}
            """;
        else
            sqlString += $"""

            update {Table.SqlTableName} set [Void] = 1{ModifiedStamp()} where [Id] = @Id;
            """;
        await _dbContext.LoadModelSqlAsync(DataSource, sqlString, dbprms =>
        {
            AddDefaultParameters(dbprms);
            // through ToString() first: the id comes out of an ExpandoObject loosely typed
            dbprms.AddTyped("@Id", PlatformId.SqlDbType,
                PlatformId.ParseId(execPrms.Get<Object>("Id")?.ToString()));
        });
    }
}

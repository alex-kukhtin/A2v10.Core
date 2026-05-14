// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    /* В журнале обязательно InOut, Document */
    /* В документе обязательно Id, Done */
    /* TODO: Find 'Document' field */
    internal async Task<IInvokeResult> ApplyDocumentAsync(ExpandoObject? prms)
    {
        var opColumn = Table.Columns.FirstOrDefault(c => c.Type == ColumnType.Operation) ?? throw new InvalidOperationException("Implement. Apply for Document");
        TableMetadata applyTable = Table.Origin ?? Table;
        if (applyTable.Apply == null || applyTable.Apply.Count == 0)
            throw new InvalidOperationException($"Table {applyTable.Schema}.[{applyTable.Table}]. Nothing to apply");

        var journals = applyTable.Apply.Select(c => c.Journal).Distinct(Comparers.ColumnReference);
        var deleteFromJournals = journals.Select(j => $"delete from {j.SqlTableName} where [Document] = @Id");

        String InsertIntoJournal(TableApply a)
        {
            // a.DetailsKind
            if (a.Mapping == null || a.Mapping.Count == 0)
                throw new InvalidOperationException("Mapping is null");
            var docAlias = "d";
            var rowsAlias = "r";

            String onUseKind = String.Empty;

            if (a.Details != null && !String.IsNullOrEmpty(a.DetailsKind))
            {
                var td = Table.Details.Select(x => x.Value).FirstOrDefault(x => x.Table == a.Details.RefTable)
                    ?? throw new InvalidOperationException($"Details {a.Details.RefTable} not found");
                var kindField = td.KindField;
                onUseKind = $" and r.[{kindField}] = N'{a.DetailsKind}'";
            }

            String applyKindAlias(ApplySourceKind kind) => kind == ApplySourceKind.Details ? rowsAlias : docAlias;

            // alias = "d" for document "r" for details
            var fields = a.Mapping.Select(m => (Target: $"[{m.Target}]", Source: $"{applyKindAlias(m.Kind)}.[{m.Source}]"));

            String JoinDetails()
            {
                if (a.Details == null || String.IsNullOrEmpty(a.Details.RefTable))
                    return String.Empty;
                return $"inner join {a.Details.SqlTableName} {rowsAlias} on {rowsAlias}.[Parent] = {docAlias}.[{Table.PrimaryKeyField}]{onUseKind}";
            }

            return $"""

                insert into {a.Journal.SqlTableName} ([InOut], {String.Join(',', fields.Select(f => f.Target))})
                select {a.InOut}, {String.Join(',', fields.Select(f => f.Source))}
                from {Table.SqlTableName} {docAlias}
                {JoinDetails()}
                where {docAlias}.Id = @Id;

            """;
        }

        var applySql = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        begin tran;
        {String.Join(";\n", deleteFromJournals)}

        {String.Join("\n\n", applyTable.Apply.Select(a => InsertIntoJournal(a)))}

        update {Table.SqlTableName} set [Done] = 1 where Id = @Id;
        commit tran;
        """;

        await _dbContext.LoadModelSqlAsync(DataSource, applySql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Id", prms?.Get<Object>(Table.PrimaryKeyField)?.ToString());
        });

        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }


    internal async Task<IInvokeResult> UnApplyDocumentAsync(ExpandoObject? prms)
    {
        var opColumn = Table.Columns.FirstOrDefault(c => c.Type == ColumnType.Operation) 
            ?? throw new InvalidOperationException("Implement. UnApply for Document");
        TableMetadata applyTable =  Table.Origin ?? Table;
        if (applyTable.Apply == null || applyTable.Apply.Count == 0)
            throw new InvalidOperationException($"Table {applyTable.Schema}.[{applyTable.Table}]. Nothing to apply");

        var journals = applyTable.Apply.Select(c => c.Journal).Distinct(Comparers.ColumnReference);

        var deleteFromJournals = journals.Select(j => 
            $"delete from {j.SqlTableName} where [Document] = @Id");

        var unApplySql = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        begin tran;
        {String.Join(";\n", deleteFromJournals)}
        
        update {Table.SqlTableName} set Done = 0 where Id = @Id;
        commit tran;

        """;

        await _dbContext.LoadModelSqlAsync(DataSource, unApplySql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Id", prms?.Get<Object>("Id")?.ToString());
        });

        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }
}

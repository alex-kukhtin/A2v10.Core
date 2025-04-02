// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;
using System.Dynamic;

namespace A2v10.Metadata;

internal partial class BaseModelBuilder
{
    private async Task<IInvokeResult> ApplyDocumentAsync(ExpandoObject? prms)
    {
        var opColumn = _table.Columns.FirstOrDefault(c => c.DataType == ColumnDataType.Operation);
        if (opColumn == null)
            throw new InvalidOperationException("Implement. Apply for Document");

        var doneColumn = _table.Columns.FirstOrDefault(c => c.Name == "Done")
            ?? throw new InvalidOperationException("Document. The 'Done' сolumn is not found");

        TableMetadata applyTable = this._baseTable ?? _table;
        if (applyTable.Apply == null || !applyTable.Apply.Any())
            throw new InvalidOperationException($"Table {applyTable.Schema}.[{applyTable.Name}]. Nothing to apply");

        var journals = applyTable.Apply.Select(c => c.Journal).Distinct(new ColumnReferenceComparer());
        var deleteFromJournals = journals.Select(j => $"delete from {j.SqlTableName} where [Document] = @Id");

        /*В журнале обязательно InOut, Date, Document */
        /*В документе обязательно Id, Done */

        /*
         * 1. Достать сюда журналы, по которым проводится документ.
         * 2. Получить соответствия полям из документа и из Details.
         * (поля документа и Details тут уже есть)
         * 3. Сформировать SQL, который проводит документ в журналах.
         * 4. В журнале ОБЯЗАТЕЛЬНО InOut, Document, Date
         */
        

        var sql = $"""
        set nocount on;
        set transaction isolation level read committed;
        declare @op {_appMeta.IdDataType};
            select @op = [{opColumn.Name}] from {_table.SqlTableName}
            where [{_appMeta.IdField}] = @Id;
        throw 60000, @op, 0;
        """;

        var applySql = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        begin tran;
        {String.Join(";\n", deleteFromJournals)}

        insert into jrn.StockJournal([Date], InOut, Document, Sum, Qty, Item)
        select d.Date, 1, d.Id, r1.[Sum], r1.Qty, r1.Item
        from doc.Rows r1
            left join doc.Documents d on r1.Parent = d.Id
        where d.Id = @Id;

        update {_table.SqlTableName} set [Done] = 1 where [{_appMeta.IdField}] = @Id;
        commit tran;
        """;

        await _dbContext.LoadModelSqlAsync(_dataSource, applySql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Id", prms?.Get<Object>("Id")?.ToString());
        });

        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }


    private async Task<IInvokeResult> UnApplyDocumentAsync(ExpandoObject? prms)
    {
        var opColumn = _table.Columns.FirstOrDefault(c => c.DataType == ColumnDataType.Operation);
        if (opColumn == null)
            throw new InvalidOperationException("Implement. UnApply for Document");
        var doneColumn = _table.Columns.FirstOrDefault(c => c.Name == "Done")
            ?? throw new InvalidOperationException("Document. The 'Done' сolumn is not found");

        TableMetadata applyTable = this._baseTable ?? _table;
        if (applyTable.Apply == null || !applyTable.Apply.Any())
            throw new InvalidOperationException($"Table {applyTable.Schema}.[{applyTable.Name}]. Nothing to apply");

        var journals = applyTable.Apply.Select(c => c.Journal).Distinct(new ColumnReferenceComparer());

        var deleteFromJournals = journals.Select(j => $"delete from {j.SqlTableName} where [Document] = @Id");

        var unApplySql = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        begin tran;
        {String.Join(";\n", deleteFromJournals)}
        
        update {_table.SqlTableName} set Done = 0 where [{_appMeta.IdField}] = @Id;
        commit tran;

        """;

        await _dbContext.LoadModelSqlAsync(_dataSource, unApplySql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Id", prms?.Get<Object>("Id")?.ToString());
        });

        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }
}

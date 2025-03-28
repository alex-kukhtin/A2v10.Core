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
            select @op = [{opColumn.Name}] from {_table.Schema}.[{_table.Name}]
            where [{_appMeta.IdField}] = @Id;
        throw 60000, @op, 0;
        """;

        var applySql = """
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        delete from jrn.StockJournal where [Document] = @Id;
        insert into jrn.StockJournal([Date], InOut, Document, Sum, Qty, Item)
        select d.Date, 1, d.Id, r1.[Sum], r1.Qty, r1.Item
        from doc.Rows r1
            left join doc.Documents d on r1.Parent = d.Id
        where d.Id = @Id;
        """;

        await _dbContext.LoadModelSqlAsync(_dataSource, applySql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Id", prms?.Get<Object>("Id")?.ToString());
        });

        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }
}

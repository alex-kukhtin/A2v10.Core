// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;

namespace A2v10.Metadata;

/* What one document put into the journals, read back. Derived from 'post' end to end - the journals
 * are its targets, the rows are found by the provenance column the posting itself requires
 * (JournalDocumentColumn), the columns are the journal's own. Nothing here is declarable, so there
 * is no form and no 'forms' key: see CLAUDE.md, "Forms: whole or nothing".
 *
 * Not the journal's index page filtered by document, which was the other candidate: this projection
 * drops what the document fixes (MemberMetadata.TransMembers), and expressing that on the journal
 * side would make a screen look different depending on who opened it.
 */
internal partial class SqlBuilder
{
    public async Task<IDataModel> LoadTransModelAsync()
    {
        var journals = Endpoint.Declaration.PostJournals().ToList();
        if (journals.Count == 0)
            throw new InvalidOperationException($"ShowTrans: {Endpoint.Path} declares no 'post'");

        // the same column the posting fills and the unpost deletes by - so a journal reachable
        // here is reachable there, and the check that it exists has already run
        String DocumentFilter(TableMetadata journal) =>
            $"[{JournalDocumentColumn(journal).Name}] = @Id";

        // what this dialog shows of a journal, and the only answer to it
        static IEnumerable<TableColumn> TransColumns(TableMetadata journal) =>
            journal.TransMembers().Select(m => m.ColumnCheck);

        String JournalRecordset(TableMetadata journal)
        {
            var fields = TransColumns(journal)
                .Select(c => c.SqlModelColumnName("a", t => t.RefTypeName));
            return $"""
            -- {journal.Path}
            select [{journal.TransName()}!{journal.TransTypeName()}!Array] = null,
              {String.Join(", ", fields)}
            from {journal.SqlTableName} a
            where a.{DocumentFilter(journal)}
            order by a.[{Constants.FieldNames.Id}];
            """;
        }

        var sb = new StringBuilder($"""
        -- transactions of {Table.Model}

        set nocount on;
        set transaction isolation level read uncommitted;

        """);
        sb.AppendLine();

        /* The shell: the document this is about, and which tab is in front. The tab is a plain
         * writable field rather than a generated template property - this dialog owns its whole
         * model, so the state has somewhere to live and the initial tab is answered once, here.
         *
         * Read from the document row so the Id is typed as the platform types it, and so a document
         * that is not there gives an empty object rather than tabs over nothing.
         */
        sb.AppendLine($"""
        select [{Constants.Trans.Root}!{Constants.Trans.TypeName}!Object] = null,
          [{Constants.FieldNames.Id}!!{Constants.FieldNames.Id}] = a.[{Constants.FieldNames.Id}],
          [{Constants.Trans.Tab}] = N'{journals[0].TransName()}'
        from {Table.SqlTableName} a where a.[{Constants.FieldNames.Id}] = @Id;
        """);
        sb.AppendLine();

        foreach (var journal in journals)
        {
            sb.AppendLine(JournalRecordset(journal));
            sb.AppendLine();
        }

        // one @map over ALL the journals: a catalog three of them point at is resolved once
        var refMap = new RefMapBuilder(
            journals.Select(j => (j, DocumentFilter(j), TransColumns(j))));
        refMap.WriteRefMap(sb);

        return await _dbContext.LoadModelSqlAsync(_descr.DataSource, sb.ToString(), dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddString("@Id", _descr.PlatformUrl.Id);
        });
    }
}

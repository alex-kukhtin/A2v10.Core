// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Dynamic;
using System.Collections.Generic;

using A2v10.Infrastructure;
using A2v10.Data.Core.Extensions;

namespace A2v10.Metadata;

internal partial class SqlBuilder
{
    // provenance column that ties journal rows to their source document; unpost deletes by it
    static TableColumn JournalDocumentColumn(TableMetadata journal) =>
        journal.Columns.FirstOrDefault(c => c.Type == ColumnType.Document)
        ?? throw new InvalidOperationException($"Journal {journal.Path}: no provenance column (ColumnType.Document)");

    internal async Task<IInvokeResult> PostDocumentAsync(ExpandoObject? prms)
    {
        var postTable = Endpoint.Declaration;
        if (postTable.Post == null || postTable.Post.Count == 0)
            throw new InvalidOperationException($"Post: {Endpoint.Path} declares no 'post'");

        // every target journal must carry a provenance column (unpost deletes its rows by it)
        foreach (var journal in postTable.Post.Select(c => c.JournalTableCheck))
            JournalDocumentColumn(journal);

        // domain = semantic type (+ target for references); SQL storage type is not compared
        static Boolean DomainMatch(TableColumn source, TableColumn target) =>
            source.Type == target.Type && (!target.IsRef || source.Target == target.Target);

        /* Names the half that disagrees and shows both sides. 'Does not match' alone sends the
         * reader to two files to find out what exactly, and the answer is always one of two things.
         */
        static String DomainDiff(String side, TableColumn source, TableColumn target) =>
            source.Type != target.Type
                ? $"{side}.[{source.Name}] is {source.Type}, journal.[{target.Name}] is {target.Type}"
                : $"{side}.[{source.Name}] targets '{source.Target}', journal.[{target.Name}] targets '{target.Target}'";

        /* One collection and some of its kinds - see PostEachMetadata. The shape is why this
         * method has no ambiguity to resolve and no spanning to reject: 'r' below is a single
         * join and the row overrides resolve against its columns, and naming two collections
         * is simply not writable.
         */
        (TableMetadata Table, String OnClause) FindDetailsTable(PostEachMetadata each)
        {
            var dt = Table.FindDetails(each.Details);
            dt.CheckKinds(each.Kinds);
            if (each.Kinds.Count == 0)
                return (dt, String.Empty);
            var kinds = String.Join(", ", each.Kinds.Select(k => $"N'{k}'"));
            return (dt, $" and r.[{dt.RowKindField}] in ({kinds})");
        }

        IEnumerable<(String Source, String Target)> CreateMapping(PostMetadata p, TableMetadata? detailsTable)
        {
            var journal = p.JournalTableCheck;
            var headerColumns = Table.AllColumns().ToList();
            var rowColumns = detailsTable?.AllColumns().ToList();

            List<(String Source, String Target)> result = [];

            foreach (var col in journal.AllColumns())
            {
                var name = col.Name;

                // baseline journal columns filled by the platform
                switch (col.Type)
                {
                    case ColumnType.Id:                             // identity
                        continue;
                    case ColumnType.Direction:                      // leg sign from 'dir'; storno never flips it
                        result.Add((p.InOutInt.ToString(), name));
                        continue;
                    case ColumnType.Document:
                        result.Add(($"d.[{Constants.FieldNames.Id}]", name));
                        continue;
                    /* Which table the Document id lives in - the one fact that cannot be recovered
                     * from the id itself, and the only reason this column exists. It becomes load
                     * bearing when documents stop sharing a single table.
                     *
                     * The storage, not the endpoint that posted. The endpoint IS recoverable: from
                     * the document row's own Operation column (filled at creation from initialValues),
                     * or trivially when a table has one endpoint. Writing it here would copy a fact
                     * that already exists into every leg of every posting - and would put an endpoint
                     * path, a behaviour-layer name that renames freely, into stored data. A storage
                     * path renames only through a migration, which the table needs anyway.
                     */
                    case ColumnType.DocumentType:
                        result.Add(($"N'{Table.Path}'", name));
                        continue;
                    case ColumnType.Row:                            // detail-row provenance; null when header-only
                        result.Add((detailsTable != null ? $"r.[{Constants.FieldNames.Id}]" : "null", name));
                        continue;
                    case ColumnType.Date:
                        result.Add(($"d.[{Constants.FieldNames.Date}]", name));
                        continue;
                }

                var isMeasure = col.Type is ColumnType.Money or ColumnType.Float or ColumnType.Decimal;
                String Signed(String expr) => p.Storno && isMeasure ? $"-{expr}" : expr;

                // 1. explicit overrides
                if (p.Document.TryGetValue(name, out var docField))
                {
                    var src = headerColumns.FirstOrDefault(c => c.Name == docField)
                        ?? throw new InvalidOperationException(
                            $"Post {Endpoint.Path} -> {journal.Path}: document field '{docField}' for [{name}] not found in {Table.Path}");
                    if (!DomainMatch(src, col))
                        throw new InvalidOperationException(
                            $"Post {Endpoint.Path} -> {journal.Path}: document field '{docField}' does not match journal column [{name}] ({DomainDiff("document", src, col)})");
                    result.Add((Signed($"d.[{docField}]"), name));
                    continue;
                }
                if (p.Row.TryGetValue(name, out var rowField))
                {
                    if (rowColumns == null)
                        throw new InvalidOperationException(
                            $"Post {Endpoint.Path} -> {journal.Path}: row mapping for [{name}] requires 'each'");
                    var src = rowColumns.FirstOrDefault(c => c.Name == rowField)
                        ?? throw new InvalidOperationException(
                            $"Post {Endpoint.Path} -> {journal.Path}: row field '{rowField}' for [{name}] not found in the rows of {Table.Path}");
                    if (!DomainMatch(src, col))
                        throw new InvalidOperationException(
                            $"Post {Endpoint.Path} -> {journal.Path}: row field '{rowField}' does not match journal column [{name}] ({DomainDiff("row", src, col)})");
                    result.Add((Signed($"r.[{rowField}]"), name));
                    continue;
                }

                // 2. auto-mapping by name + domain
                var inHeader = headerColumns.FirstOrDefault(c => c.Name == name);
                var inRow = rowColumns?.FirstOrDefault(c => c.Name == name);

                if (inHeader != null && !DomainMatch(inHeader, col))
                    throw new InvalidOperationException(
                        $"Post {Endpoint.Path} -> {journal.Path}: [{name}] exists in {Table.Path} but the domain differs ({DomainDiff("document", inHeader, col)}); map it explicitly in 'document'");
                if (inRow != null && !DomainMatch(inRow, col))
                    throw new InvalidOperationException(
                        $"Post {Endpoint.Path} -> {journal.Path}: [{name}] exists in the rows of {Table.Path} but the domain differs ({DomainDiff("row", inRow, col)}); map it explicitly in 'row'");

                var hasHeader = inHeader != null;
                var hasRow = inRow != null;

                if (hasHeader && hasRow)
                    throw new InvalidOperationException(
                        $"Post {Endpoint.Path} -> {journal.Path}: [{name}] is ambiguous (present in both header and rows); disambiguate in 'document' or 'row'");
                if (hasHeader)
                    result.Add((Signed($"d.[{name}]"), name));
                else if (hasRow)
                    result.Add((Signed($"r.[{name}]"), name));
                else
                    throw new InvalidOperationException(
                        $"Post {Endpoint.Path} -> {journal.Path}: cannot resolve journal column [{name}] in {Table.Path} or its rows");
            }
            return result;
        }

        String InsertIntoJournal(PostMetadata p)
        {
            var journal = p.JournalTableCheck;
            TableMetadata? detailsTable = null;
            var join = String.Empty;
            if (p.Each != null)
            {
                var (dt, onClause) = FindDetailsTable(p.Each);
                detailsTable = dt;
                join = $"inner join {dt.SqlTableName} r on r.[{Constants.FieldNames.Owner}] = d.[{Constants.FieldNames.Id}]{onClause}";
            }

            var map = CreateMapping(p, detailsTable).ToList();
            if (map.Count == 0)
                throw new InvalidOperationException($"Post {Endpoint.Path} -> {journal.Path}: mapping is empty");

            return $"""

                insert into {journal.SqlTableName} ({String.Join(", ", map.Select(m => $"[{m.Target}]"))})
                select {String.Join(", ", map.Select(m => m.Source))}
                from {Table.SqlTableName} d
                {join}
                where d.[{Constants.FieldNames.Id}] = @Id;

            """;
        }

        var postSql = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        declare @Done bit;
        select @Done = [Done] from {Table.SqlTableName} where Id = @Id;
        if @Done = 1
            throw 600000, N'@[Error.Document.AlreadyPosted]', 0;

        begin tran;
        {String.Join("\n\n", postTable.Post.Select(InsertIntoJournal))}

        update {Table.SqlTableName} set [Done] = 1 where Id = @Id;
        commit tran;
        """;

        await _dbContext.LoadModelSqlAsync(DataSource, postSql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Id", prms?.Get<Object>("Id")?.ToString());
        });

        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }


    internal async Task<IInvokeResult> UnPostDocumentAsync(ExpandoObject? prms)
    {
        var postTable = Endpoint.Declaration;
        if (postTable.Post == null || postTable.Post.Count == 0)
            throw new InvalidOperationException($"Endpoint {Endpoint.Path}. Nothing to UnPost");

        var journals = postTable.Post.Select(c => c.JournalTableCheck).DistinctBy(j => j.SqlTableName);

        var deleteFromJournals = journals.Select(j =>
            $"delete from {j.SqlTableName} where [{JournalDocumentColumn(j).Name}] = @Id");

        var unPostSql = $"""
        set nocount on;
        set transaction isolation level read committed;
        set xact_abort on;

        declare @Done bit;
        select @Done = [Done] from {Table.SqlTableName} where Id = @Id;
        if @Done = 0
            throw 600000, N'@[Error.Document.NotPosted]', 0;

        begin tran;
        {String.Join(";\n", deleteFromJournals)}

        update {Table.SqlTableName} set Done = 0 where Id = @Id;
        commit tran;

        """;

        await _dbContext.LoadModelSqlAsync(DataSource, unPostSql, dbprms =>
        {
            dbprms.AddBigInt("@UserId", _currentUser.Identity.Id)
            .AddString("@Id", prms?.Get<Object>("Id")?.ToString());
        });

        return EmptyInvokeResult.FromString("{}", MimeTypes.Application.Json);
    }
}

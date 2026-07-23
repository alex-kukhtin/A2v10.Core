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
        ?? throw new InvalidOperationException($"Journal '{journal.Table}': no provenance column (ColumnType.Document)");

    internal async Task<IInvokeResult> PostDocumentAsync(ExpandoObject? prms)
    {
        TableMetadata postTable = Table.Origin ?? Table;
        if (postTable.Post == null || postTable.Post.Count == 0)
            throw new InvalidOperationException($"Table '{Table.Table}'. Nothing to post");

        // every target journal must carry a provenance column (unpost deletes its rows by it)
        foreach (var journal in postTable.Post.Select(c => c.JournalTableCheck))
            JournalDocumentColumn(journal);

        // domain = semantic type (+ target for references); SQL storage type is not compared
        static Boolean DomainMatch(TableColumn source, TableColumn target) =>
            source.Type == target.Type && (!target.IsRef || source.Target == target.Target);

        // 'each' names either a kind-less collection directly, or the kinds of a single collection
        (TableMetadata Table, String OnClause) FindDetailsTable(PostMetadata p)
        {
            var journal = p.JournalTableCheck;
            if (p.Each.Count == 1
                && Table.Details.TryGetValue(p.Each[0], out var direct)
                && direct.Kinds.Count == 0)
                return (direct, String.Empty);

            var byKind = Table.Details.Values
                .Where(d => d.Kinds.Count > 0 && p.Each.All(k => d.Kinds.Contains(k)))
                .ToList();
            if (byKind.Count == 0)
                throw new InvalidOperationException(
                    $"Post to '{journal.Table}': cannot resolve each [{String.Join(", ", p.Each)}] to a details collection of {Table.SqlTableName}");
            if (byKind.Count > 1)
                throw new InvalidOperationException(
                    $"Post to '{journal.Table}': each [{String.Join(", ", p.Each)}] is ambiguous across details collections of {Table.SqlTableName}");

            var dt = byKind[0];
            var kinds = String.Join(", ", p.Each.Select(k => $"N'{k}'"));
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
                    case ColumnType.DocumentType:                   // provenance discriminator: source endpoint
                        result.Add(($"N'{postTable.Path}'", name));
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
                            $"Post to '{journal.Table}': document field '{docField}' for [{name}] not found in {Table.SqlTableName}");
                    if (!DomainMatch(src, col))
                        throw new InvalidOperationException(
                            $"Post to '{journal.Table}': domain of document field '{docField}' does not match journal column [{name}]");
                    result.Add((Signed($"d.[{docField}]"), name));
                    continue;
                }
                if (p.Row.TryGetValue(name, out var rowField))
                {
                    if (rowColumns == null)
                        throw new InvalidOperationException(
                            $"Post to '{journal.Table}': row mapping for [{name}] requires 'each'");
                    var src = rowColumns.FirstOrDefault(c => c.Name == rowField)
                        ?? throw new InvalidOperationException(
                            $"Post to '{journal.Table}': row field '{rowField}' for [{name}] not found in {detailsTable!.SqlTableName}");
                    if (!DomainMatch(src, col))
                        throw new InvalidOperationException(
                            $"Post to '{journal.Table}': domain of row field '{rowField}' does not match journal column [{name}]");
                    result.Add((Signed($"r.[{rowField}]"), name));
                    continue;
                }

                // 2. auto-mapping by name + domain
                var inHeader = headerColumns.FirstOrDefault(c => c.Name == name);
                var inRow = rowColumns?.FirstOrDefault(c => c.Name == name);

                if (inHeader != null && !DomainMatch(inHeader, col))
                    throw new InvalidOperationException(
                        $"Post to '{journal.Table}': [{name}] exists in {Table.SqlTableName} but domain does not match; map it explicitly in 'document'");
                if (inRow != null && !DomainMatch(inRow, col))
                    throw new InvalidOperationException(
                        $"Post to '{journal.Table}': [{name}] exists in {detailsTable!.SqlTableName} but domain does not match; map it explicitly in 'row'");

                var hasHeader = inHeader != null;
                var hasRow = inRow != null;

                if (hasHeader && hasRow)
                    throw new InvalidOperationException(
                        $"Post to '{journal.Table}': [{name}] is ambiguous (present in both header and rows); disambiguate in 'document' or 'row'");
                if (hasHeader)
                    result.Add((Signed($"d.[{name}]"), name));
                else if (hasRow)
                    result.Add((Signed($"r.[{name}]"), name));
                else
                    throw new InvalidOperationException(
                        $"Post to '{journal.Table}': cannot resolve journal column [{name}] in {Table.SqlTableName} or its rows");
            }
            return result;
        }

        String InsertIntoJournal(PostMetadata p)
        {
            var journal = p.JournalTableCheck;
            TableMetadata? detailsTable = null;
            var join = String.Empty;
            if (p.Each.Count > 0)
            {
                var (dt, onClause) = FindDetailsTable(p);
                detailsTable = dt;
                join = $"inner join {dt.SqlTableName} r on r.[{Constants.FieldNames.Owner}] = d.[{Constants.FieldNames.Id}]{onClause}";
            }

            var map = CreateMapping(p, detailsTable).ToList();
            if (map.Count == 0)
                throw new InvalidOperationException($"Post to '{journal.Table}': mapping is empty");

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
        TableMetadata postTable =  Table.Origin ?? Table;
        if (postTable.Post == null || postTable.Post.Count == 0)
            throw new InvalidOperationException($"Table {postTable.Schema}.[{postTable.Table}]. Nothing to UnPost");

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

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* What posting and unposting DO, as the body of a statement - the inserts and deletes the platform
 * maps, or the procedures that replace them. The frame around it (the transaction, the Done lock)
 * is the same either way and lives in SqlBuilderPost, which is why this hands out bodies and not a
 * script: the two spellings differ in the body alone.
 *
 * Built at load (ResolvePostAsync) so a wrong 'post' fails there and not under the button. Rebuilt
 * per invocation rather than kept: the endpoint is never mutated after publication.
 */
internal sealed class PostStatements
{
    private readonly NormalEndpointMetadata _endpoint;
    private readonly TableMetadata _table;
    private readonly List<PostMetadata> _post;

    internal PostStatements(NormalEndpointMetadata endpoint)
    {
        _endpoint = endpoint;
        _table = endpoint.Storage;
        _post = endpoint.Declaration.Post is { Count: > 0 } post
            ? post
            : throw new InvalidOperationException($"Post: {endpoint.Path} declares no 'post'");

        /* Whoever writes the rows: the dialog filters by provenance, and so does an unpost that was
         * not declared. A journal without it takes rows nothing can find again.
         */
        foreach (var journal in _post.SelectMany(p => p.Targets))
            JournalDocumentColumn(journal);

        // CheckPost has run: a 'sql' entry is the only entry, so the first one answers for the list
        var sql = _post[0].Sql;
        Post = sql != null
            ? Exec(sql.Post)
            : String.Join("\n\n", _post.Select(InsertIntoJournal));
        UnPost = sql?.UnPost != null
            ? Exec(sql.UnPost)
            : String.Join("\n", _endpoint.Declaration.PostJournals()
                .Select(j => $"delete from {j.SqlTableName} where {DocumentFilter(j, _table, String.Empty)};"));
    }

    // both run inside the platform's transaction, after the Done flag has been claimed
    internal String Post { get; }
    internal String UnPost { get; }

    /* The ports, and all of them. Anything else a procedure needs it reads from the document it was
     * handed - a parameter added here is a fact the procedure would take without declaring it.
     *
     * The name goes in as written: nothing validates it (see CLAUDE.md), and quoting it here would
     * add a second spelling rule for something the database already answers.
     */
    private static String Exec(String procedure) => $"exec {procedure} @Id = @Id, @UserId = @UserId;";

    private static TableColumn JournalDocumentColumn(TableMetadata journal) =>
        journal.AllColumns().FirstOrDefault(c => c.Type == ColumnType.Document)
        ?? throw new InvalidOperationException($"Journal {journal.Path}: no provenance column (ColumnType.Document)");

    /* The storage, not the endpoint that posted: the endpoint is recoverable from the row's own
     * Operation column, and it is a behaviour-layer name that renames freely. A storage path
     * renames only through a migration. One function because the insert writes it and the filter
     * compares it.
     */
    private static String DocumentTypeValue(TableMetadata document) => $"N'{document.Path}'";

    /* Provenance: the id, plus the table it lives in where the journal carries the discriminator.
     * Ids are per table, so two documents declaring 'table' both have a row 5 and by the id alone
     * an unpost of one deletes the rows of the other.
     *
     * The prefix goes on EVERY term: '@map' selects from a bare table, the recordset from an
     * aliased one.
     */
    internal static String DocumentFilter(TableMetadata journal, TableMetadata document, String prefix)
    {
        var byDocument = $"{prefix}[{JournalDocumentColumn(journal).Name}] = @Id";
        var docType = journal.AllColumns().FirstOrDefault(c => c.Type == ColumnType.DocumentType);
        return docType == null
            ? byDocument
            : $"{byDocument} and {prefix}[{docType.Name}] = {DocumentTypeValue(document)}";
    }

    // domain = semantic type (+ target for references); SQL storage type is not compared
    private static Boolean DomainMatch(TableColumn source, TableColumn target) =>
        source.Type == target.Type && (!target.IsRef || source.Target == target.Target);

    // names the half that disagrees: 'does not match' alone sends the reader to two files
    private static String DomainDiff(String side, TableColumn source, TableColumn target) =>
        source.Type != target.Type
            ? $"{side}.[{source.Name}] is {source.Type}, journal.[{target.Name}] is {target.Type}"
            : $"{side}.[{source.Name}] targets '{source.Target}', journal.[{target.Name}] targets '{target.Target}'";

    /* No 'dir' writes 0 - neither in nor out, and indistinguishable from a declared value. Asked
     * of the JOURNAL: without the column both legs ride in the sign of the measure.
     */
    private void CheckDirection(PostMetadata p, TableMetadata journal)
    {
        if (p.Dir != PostDirection.None)
            return;
        if (!journal.AllColumns().Any(c => c.Type == ColumnType.Direction))
            return;
        throw new InvalidOperationException(
            $"Post {_endpoint.Path} -> {journal.Path}: the journal has a Direction column, so 'dir' is required ('in' or 'out')");
    }

    // one collection and some of its kinds - naming two is not writable, see PostEachMetadata
    private (TableMetadata Table, String OnClause) FindDetailsTable(PostEachMetadata each)
    {
        var dt = _table.FindDetails(each.Details);
        dt.CheckKinds(each.Kinds);
        if (each.Kinds.Count == 0)
            return (dt, String.Empty);
        var kinds = String.Join(", ", each.Kinds.Select(k => $"N'{k}'"));
        return (dt, $" and r.[{dt.RowKindField}] in ({kinds})");
    }

    private IEnumerable<(String Source, String Target)> CreateMapping(PostMetadata p, TableMetadata? detailsTable)
    {
        var journal = p.JournalTableCheck;
        var headerColumns = _table.AllColumns().ToList();
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
                case ColumnType.DocumentType:
                    result.Add((DocumentTypeValue(_table), name));
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
                        $"Post {_endpoint.Path} -> {journal.Path}: document field '{docField}' for [{name}] not found in {_table.Path}");
                if (!DomainMatch(src, col))
                    throw new InvalidOperationException(
                        $"Post {_endpoint.Path} -> {journal.Path}: document field '{docField}' does not match journal column [{name}] ({DomainDiff("document", src, col)})");
                result.Add((Signed($"d.[{docField}]"), name));
                continue;
            }
            if (p.Row.TryGetValue(name, out var rowField))
            {
                if (rowColumns == null)
                    throw new InvalidOperationException(
                        $"Post {_endpoint.Path} -> {journal.Path}: row mapping for [{name}] requires 'each'");
                var src = rowColumns.FirstOrDefault(c => c.Name == rowField)
                    ?? throw new InvalidOperationException(
                        $"Post {_endpoint.Path} -> {journal.Path}: row field '{rowField}' for [{name}] not found in the rows of {_table.Path}");
                if (!DomainMatch(src, col))
                    throw new InvalidOperationException(
                        $"Post {_endpoint.Path} -> {journal.Path}: row field '{rowField}' does not match journal column [{name}] ({DomainDiff("row", src, col)})");
                result.Add((Signed($"r.[{rowField}]"), name));
                continue;
            }

            /* 2. auto-mapping by name + domain. The ROW wins: under 'each' the header's value is
             * the document total written onto every line. Refused as ambiguous before, and the
             * refusal was reachable only here - without 'each' there are no row columns to collide
             * with, so it asked the author to disambiguate in the one case that has an answer.
             */
            var inRow = rowColumns?.FirstOrDefault(c => c.Name == name);
            var inHeader = headerColumns.FirstOrDefault(c => c.Name == name);

            var (side, alias, taken) = inRow != null ? ("row", "r", inRow)
                : inHeader != null ? ("document", "d", inHeader)
                : throw new InvalidOperationException(
                    $"Post {_endpoint.Path} -> {journal.Path}: cannot resolve journal column [{name}] in {_table.Path} or its rows");

            // of the column actually taken, not of both: the one not chosen is not being written
            if (!DomainMatch(taken, col))
                throw new InvalidOperationException(
                    $"Post {_endpoint.Path} -> {journal.Path}: [{name}] exists in {(inRow != null ? $"the rows of {_table.Path}" : _table.Path)} but the domain differs ({DomainDiff(side, taken, col)}); map it explicitly in '{side}'");

            result.Add((Signed($"{alias}.[{name}]"), name));
        }
        return result;
    }

    private String InsertIntoJournal(PostMetadata p)
    {
        var journal = p.JournalTableCheck;
        CheckDirection(p, journal);

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
            throw new InvalidOperationException($"Post {_endpoint.Path} -> {journal.Path}: mapping is empty");

        return $"""
            insert into {journal.SqlTableName} ({String.Join(", ", map.Select(m => $"[{m.Target}]"))})
            select {String.Join(", ", map.Select(m => m.Source))}
            from {_table.SqlTableName} d
            {join}
            where d.[{Constants.FieldNames.Id}] = @Id;
            """;
    }
}

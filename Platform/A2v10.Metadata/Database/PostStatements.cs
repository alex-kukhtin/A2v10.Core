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

    internal PostStatements(NormalEndpointMetadata endpoint)
    {
        _endpoint = endpoint;
        _table = endpoint.Storage;
        var posts = endpoint.Declaration.Posts().ToList();
        if (posts.Count == 0)
            throw new InvalidOperationException($"Post: {endpoint.Path} declares no 'post'");

        /* Whoever writes the rows: the dialog filters by provenance, and so does an unpost that was
         * not declared. A journal without it takes rows nothing can find again.
         */
        foreach (var journal in posts.SelectMany(p => p.Post).SelectMany(p => p.Targets))
            JournalDocumentColumn(journal);

        if (posts is [(null, var own)])
        {
            (Post, UnPost) = Bodies(own);
            return;
        }

        /* One body per operation, chosen by the code the document carries when it is posted. The
         * switch costs the server nothing: the code is read at the moment of posting, and a posted
         * document is read-only, so the unpost meets the same code the post did.
         */
        var bodies = posts.Select(p => (p.Operation!.Id, Bodies: Bodies(p.Post))).ToList();
        Post = Dispatch(bodies.Select(b => (b.Id, b.Bodies.Post)));
        UnPost = Dispatch(bodies.Select(b => (b.Id, b.Bodies.UnPost)));
    }

    // CheckPost has run: a 'sql' entry is the only entry, so the first one answers for the list
    private (String Post, String UnPost) Bodies(List<PostMetadata> post)
    {
        var sql = post[0].Sql;
        var postBody = sql != null
            ? Exec(sql.Post)
            : String.Join("\n\n", post.Select(p => p.IsLedger ? InsertIntoLedger(p) : InsertIntoJournal(p)));
        var unPostBody = sql?.UnPost != null
            ? Exec(sql.UnPost)
            : String.Join("\n", post.SelectMany(p => p.Targets).DistinctBy(j => j.SqlTableName)
                .Select(j => $"delete from {j.SqlTableName} where {DocumentFilter(j, _table, String.Empty)};"));
        return (postBody, unPostBody);
    }

    /* A code the endpoint does not declare is refused rather than posted as nothing: it is a document
     * whose operation was removed from 'operations' after it was saved, and an empty posting would
     * mark it Done with no movements.
     */
    private String Dispatch(IEnumerable<(String Id, String Body)> bodies)
    {
        var column = _table.AllColumns().First(c => c.IsOperation);
        var branches = String.Join("\nelse ", bodies.Select(b => $"""
            if @OperationId = {column.SqlLiteral(b.Id)}
            begin
            {b.Body}
            end
            """));
        return $"""
            declare @OperationId {column.SqlDataType()} = (select [{column.Name}] from {_table.SqlTableName} where [{Constants.FieldNames.Id}] = @Id);
            {branches}
            else
                throw 600000, N'The operation of the document is not one of {_endpoint.Path}', 0;
            """;
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
        var journal = p.TargetTableCheck;
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

    // the rows a posting iterates: none without 'each', one join with it
    private (TableMetadata? Table, String Join) EachJoin(PostMetadata p)
    {
        if (p.Each == null)
            return (null, String.Empty);
        var (dt, onClause) = FindDetailsTable(p.Each);
        return (dt, $"inner join {dt.SqlTableName} r on r.[{dt.MasterField}] = d.[{Constants.FieldNames.Id}]{onClause}");
    }

    private String InsertIntoJournal(PostMetadata p)
    {
        var journal = p.TargetTableCheck;
        CheckDirection(p, journal);

        var (detailsTable, join) = EachJoin(p);

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

    /* A posting into a ledger: one declaration, two rows. The CTE reads the document once and carries
     * both legs side by side - [Dt$X] and [Ct$X], '$' because the platform composes these names and an
     * author name cannot hold one. The insert selects it twice, the second time mirrored: Acc and
     * CorrAcc swap, the analytics of the other leg are taken, the sum is the same - so debit equals
     * credit by construction and nothing checks it.
     *
     * Nothing is mapped by name: a name does not know its leg and would fill both rows. The legs name
     * Acc and the author's columns; the provenance is found by type, the rest of the baseline is the
     * platform's.
     */
    private String InsertIntoLedger(PostMetadata p)
    {
        var ledger = p.TargetTableCheck;
        var head = $"Post {_endpoint.Path} -> {ledger.Path}";
        var (detailsTable, join) = EachJoin(p);
        var headerColumns = _table.AllColumns().ToList();
        var rowColumns = detailsTable?.AllColumns().ToList();

        TableColumn Source(String block, String field, String name)
        {
            var columns = block == "row"
                ? rowColumns ?? throw new InvalidOperationException($"{head}: 'row' for [{name}] requires 'each'")
                : headerColumns;
            return columns.FirstOrDefault(c => c.Name == field)
                ?? throw new InvalidOperationException(
                    $"{head}: {block} field '{field}' for [{name}] not found in {(block == "row" ? $"the rows of {_table.Path}" : _table.Path)}");
        }

        String Expr(String block, String field, TableColumn col)
        {
            if (block == "const")
            {
                try
                {
                    return col.SqlLiteral(field);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException($"{head}: const [{col.Name}] - {ex.Message}", ex);
                }
            }
            var src = Source(block, field, col.Name);
            if (!DomainMatch(src, col))
                throw new InvalidOperationException(
                    $"{head}: {block} field '{field}' does not match ledger column [{col.Name}] ({DomainDiff(block, src, col)})");
            return $"{(block == "row" ? "r" : "d")}.[{field}]";
        }

        // what a leg may name: its account and the author's own columns - not the provenance
        TableColumn LegColumn(String leg, String name)
        {
            var col = ledger.AllColumns().FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException($"{head}: '{leg}' names [{name}], which is not a column of the ledger");
            var legal = name == Constants.FieldNames.Acc
                || ledger.Columns.Contains(col) && col.Type is not (ColumnType.Document or ColumnType.DocumentType
                    or ColumnType.Row or ColumnType.Operation);
            return legal ? col
                : throw new InvalidOperationException($"{head}: '{leg}' names [{name}], which the platform fills");
        }

        Dictionary<String, String> Leg(String leg, PostLegMetadata blocks)
        {
            var map = new Dictionary<String, String>();
            foreach (var (block, entries) in new[] { ("const", blocks.Const), ("document", blocks.Document), ("row", blocks.Row) })
                foreach (var (name, field) in entries)
                {
                    var col = LegColumn(leg, name);
                    if (!map.TryAdd(name, Expr(block, field, col)))
                        throw new InvalidOperationException($"{head}: '{leg}' names [{name}] in two blocks");
                }
            if (!map.ContainsKey(Constants.FieldNames.Acc))
                throw new InvalidOperationException($"{head}: '{leg}' declares no [{Constants.FieldNames.Acc}] - one account per leg, in any block");
            return map;
        }

        var dt = Leg("dt", p.Dt!);
        var ct = Leg("ct", p.Ct!);

        // the sum is of the rows under 'each', of the header otherwise - one per posting either way
        var sumCol = ledger.AllColumns().First(c => c.Name == Constants.FieldNames.Sum);
        var sum = Expr(detailsTable != null ? "row" : "document", p.Sum!, sumCol);

        // one value for both legs: the date and the document's provenance
        List<(String Target, String Source)> common = [(Constants.FieldNames.Date, $"d.[{Constants.FieldNames.Date}]")];
        foreach (var col in ledger.Columns)
        {
            String? source = col.Type switch
            {
                ColumnType.Document => $"d.[{Constants.FieldNames.Id}]",
                ColumnType.DocumentType => DocumentTypeValue(_table),
                // a document without operations posts none
                ColumnType.Operation => headerColumns.FirstOrDefault(c => c.IsOperation) is { } op ? $"d.[{op.Name}]" : "null",
                _ => null
            };
            if (source != null)
                common.Add((col.Name, source));
        }

        /* The row is the provenance of a LEG, not of the posting: a leg that takes nothing from the row
         * is the header's, carries no row and collapses - its rows differ in nothing but the sum, so
         * they group into one. A leg that reads the row stays one per row. Everything a collapsed leg
         * holds besides the sum is a constant or a header value, so grouping by it is exact; the sum
         * of both legs is still read from the same rows, and debit equals credit as before.
         */
        var rowColumn = ledger.Columns.FirstOrDefault(c => c.Type == ColumnType.Row);
        Boolean ReadsRow(PostLegMetadata leg) => detailsTable != null && leg.Row.Count > 0;

        // the author's columns either leg names, in the ledger's order; the other leg writes null there
        var analytics = ledger.Columns.Select(c => c.Name)
            .Where(n => dt.ContainsKey(n) || ct.ContainsKey(n)).ToList();

        String Both(String name) =>
            $"[Dt${name}] = {dt.GetValueOrDefault(name, "null")}, [Ct${name}] = {ct.GetValueOrDefault(name, "null")}";

        var cte = common.Select(c => $"[{c.Target}] = {c.Source}")
            .Concat(rowColumn != null && detailsTable != null ? [$"[{rowColumn.Name}] = r.[{Constants.FieldNames.Id}]"] : [])
            .Append($"[{Constants.FieldNames.Sum}] = {sum}")
            .Append(Both(Constants.FieldNames.Acc))
            .Concat(analytics.Select(Both));

        var targets = common.Select(c => c.Target)
            .Concat(rowColumn != null ? [rowColumn.Name] : [])
            .Concat([Constants.FieldNames.InOut, Constants.FieldNames.Acc, Constants.FieldNames.CorrAcc, Constants.FieldNames.Sum])
            .Concat(analytics);

        // one leg: every column but the sum is the key it collapses by
        String Select(String inOut, String self, String other, PostLegMetadata leg)
        {
            var keys = common.Select(c => $"[{c.Target}]")
                .Concat(rowColumn != null && ReadsRow(leg) ? [$"[{rowColumn.Name}]"] : [])
                .Concat([$"[{self}${Constants.FieldNames.Acc}]", $"[{other}${Constants.FieldNames.Acc}]"])
                .Concat(analytics.Select(n => $"[{self}${n}]"))
                .ToList();
            var row = rowColumn == null ? [] : ReadsRow(leg) ? new[] { $"[{rowColumn.Name}]" } : ["null"];
            var fields = common.Select(c => $"[{c.Target}]")
                .Concat(row)
                .Concat([inOut, $"[{self}${Constants.FieldNames.Acc}]", $"[{other}${Constants.FieldNames.Acc}]", $"sum([{Constants.FieldNames.Sum}])"])
                .Concat(analytics.Select(n => $"[{self}${n}]"));
            return $"select {String.Join(", ", fields)} from P group by {String.Join(", ", keys)}";
        }

        return $"""
            with P as (
                select {String.Join(",\n        ", cte)}
                from {_table.SqlTableName} d
                {join}
                where d.[{Constants.FieldNames.Id}] = @Id
            )
            insert into {ledger.SqlTableName} ({String.Join(", ", targets.Select(t => $"[{t}]"))})
            {Select("1", "Dt", "Ct", p.Dt!)}
            union all
            {Select("-1", "Ct", "Dt", p.Ct!)};
            """;
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* The rows of one collection a birth copies: the target's collection, the source's of the same name,
 * and which kind of the target each row lands in - with the source kind it comes from, or null when
 * the source has none and every row goes to the one kind named.
 */
internal sealed record BasedOnRows(TableMetadata Target, TableMetadata Source, IReadOnlyList<(String? Kind, String? SourceKind)> Kinds);

/* What a document born on basis takes from its source, column by column. One law asked twice: at the
 * source's load, which refuses a file that cannot be copied (DatabaseMetadataProvider.ResolveBasedOnAsync),
 * and at birth, which writes the copy (SqlBuilder.BirthPrelude). The target's columns are the list;
 * each takes a column of the source or nothing, and nothing is the target's own start.
 */
internal static class BasedOnMapping
{
    /* Only what the author declared, on both sides: a baseline column is the record's own (its key,
     * its date, whether it is posted), and so are the number, the operation and the kind of a row -
     * the platform, the url and 'each' give those. A name meeting a column of another domain is passed
     * over, not refused: nothing in the file could exclude it, and the card shows the gap.
     */
    internal static TableColumn? SourceOf(TableColumn target, TableMetadata targetTable, TableMetadata sourceTable,
        IReadOnlyDictionary<String, String> overrides)
    {
        if (!targetTable.Columns.Contains(target) || IsIssued(target))
            return null;
        if (overrides.TryGetValue(target.Name, out var name))
            return sourceTable.Columns.First(c => c.Name == name);
        return sourceTable.Columns.FirstOrDefault(c => c.Name == target.Name && PostStatements.DomainMatch(c, target));
    }

    private static Boolean IsIssued(TableColumn column) =>
        column.Type is ColumnType.Autonum or ColumnType.Operation or ColumnType.RowKind;

    // 'document' names a column on each side, of one domain; an issued one would be dropped by SourceOf without a word
    internal static void CheckDocument(String head, BasedOnMetadata entry, TableMetadata targetTable, TableMetadata sourceTable)
    {
        foreach (var (to, from) in entry.Document)
        {
            var target = targetTable.Columns.FirstOrDefault(c => c.Name == to)
                ?? throw new InvalidOperationException($"{head}: 'document' names [{to}], which {targetTable.Path} does not declare");
            if (IsIssued(target))
                throw new InvalidOperationException($"{head}: 'document' names [{to}], a {target.Type} - it is issued to the new document, never copied");
            var source = sourceTable.Columns.FirstOrDefault(c => c.Name == from)
                ?? throw new InvalidOperationException($"{head}: 'document' takes [{to}] from [{from}], which {sourceTable.Path} does not declare");
            if (!PostStatements.DomainMatch(source, target))
                throw new InvalidOperationException(source.Type != target.Type
                    ? $"{head}: 'document' takes [{to}] ({target.Type}) from [{from}] ({source.Type})"
                    : $"{head}: 'document' takes [{to}] (targets '{target.Target}') from [{from}] (targets '{source.Target}')");
        }
    }

    /* 'each' is the target's collection and kinds, as 'post' has it (TableMetadata.CheckKinds); the rows
     * come from the source's collection of the same name. Kinds meet by name, and a source without kinds
     * pours into the one kind named. A source with kinds into a target without - which of its rows go is
     * written nowhere, so it is refused rather than guessed.
     */
    internal static BasedOnRows? Rows(String head, BasedOnMetadata entry, TableMetadata targetTable, TableMetadata sourceTable)
    {
        if (entry.Each is not { } each)
            return null;
        var target = targetTable.FindDetails(each.Details);
        target.CheckKinds(each.Kinds);
        if (!sourceTable.Details.TryGetValue(each.Details, out var source))
            throw new InvalidOperationException(
                $"{head}: 'each' names '{each.Details}', and {sourceTable.Path} has no collection of that name to copy from");
        if (target.Kinds.Count == 0)
        {
            if (source.Kinds.Count > 0)
                throw new InvalidOperationException(
                    $"{head}: '{each.Details}' of {sourceTable.Path} has kinds and that of {targetTable.Path} has none - which rows go is written nowhere");
            return new(target, source, [(null, null)]);
        }
        if (source.Kinds.Count == 0)
        {
            if (each.Kinds.Count != 1)
                throw new InvalidOperationException(
                    $"{head}: '{each.Details}' of {sourceTable.Path} has no kinds, so its rows go into one kind - name one in 'kinds'");
            return new(target, source, [(each.Kinds[0], null)]);
        }
        if (each.Kinds.FirstOrDefault(k => !source.Kinds.ContainsKey(k)) is { } missing)
            throw new InvalidOperationException(
                $"{head}: kind '{missing}' of '{each.Details}' is not a kind of {sourceTable.Path}. Its kinds: {String.Join(", ", source.Kinds.Keys)}");
        return new(target, source, [.. each.Kinds.Select(k => ((String?)k, (String?)k))]);
    }
}

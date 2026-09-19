// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using A2v10.Infrastructure;
using A2v10.Services;

namespace A2v10.Metadata;

internal static class TableColumnPredicates
{
    internal static Boolean IsIndexColumn(TableColumn col)
        => col.Type != ColumnType.RowVersion && col.Type != ColumnType.Void && col.Type != ColumnType.IsSystem
            && !col.IsStamp;
    internal static Boolean IsEditColumn(TableColumn col)
        => col.Type != ColumnType.RowVersion && col.Type != ColumnType.Void && col.Type != ColumnType.IsSystem
            && col.Type != ColumnType.Id && col.Type != ColumnType.Done && !col.IsStamp;
    /* What the client sends: the table type and the DataTable filling it are built from this one
     * answer, since their column order must match. A stamp is written by the statement itself.
     */
    internal static Boolean IsSentColumn(TableColumn col)
        => !col.IsStamp;
}

internal static class MetadataExtensions
{
    internal static EndpointKind ToEndpointKind(this String schema)
    {
        return schema switch
        {
            Constants.SchemaNames.Catalog => EndpointKind.Catalog,
            Constants.SchemaNames.Document => EndpointKind.Document,
            Constants.SchemaNames.Journal => EndpointKind.Journal,
            Constants.SchemaNames.Report => EndpointKind.Report,
            Constants.SchemaNames.Enum => EndpointKind.Enum,
            Constants.SchemaNames.State => EndpointKind.State,
            Constants.SchemaNames.Autonum => EndpointKind.Autonum,
            Constants.SchemaNames.AccPlan => EndpointKind.AccPlan,
            Constants.SchemaNames.Ledger => EndpointKind.Ledger,
            _ => throw new InvalidOperationException($"Invalid schema for EndpointKind '{schema}'")
        };
    }

    internal static String ToSqlSchema(this String folder)
    {
        return folder switch
        {
            Constants.SchemaNames.Catalog => "cat",
            Constants.SchemaNames.Document => "doc",
            Constants.SchemaNames.Journal => "jrn",
            Constants.SchemaNames.Report => "rep",
            Constants.SchemaNames.Enum => "enm",
            /* Not shortened, and that is the rule rather than an exception: the names above
             * abbreviate long words, and 'state' is already short - 'sta' would cost the reader of
             * generated SQL a decoding and buy nothing. Written out all the same: a folder falling
             * through to '_' is indistinguishable from a schema name an author wrote in the file,
             * so only a line here says this was decided.
             */
            Constants.SchemaNames.State => "state",
            Constants.SchemaNames.AccPlan => "acc",
            Constants.SchemaNames.Ledger => "led",
            "account" => "acc",
            "inforegister" => "regi",
            _ => folder
        };
    }

    // the address is the endpoint's, not the table's: several endpoints share one table
    public static IPlatformUrl PlatformUrl(this NormalEndpointMetadata endpoint, String action)
    {
        var kind = action == "index" || action == "edit" && endpoint.Storage.EditWithPage ? "_page" : "_dialog";
        var url = $"{kind}{endpoint.Path}/{action}/".ToLowerInvariant();
        return new PlatformUrl(url);
    }

    internal static IEnumerable<TableColumn> AllColumns(this TableMetadata table, Func<TableColumn, Boolean>? predicate = null) =>
        table.DefaultColumns.Concat(table.Columns).Where(predicate ?? (_ => true));

    /* Everything about a table that the seed cannot say in columns, as one fingerprint. The deploy
     * hash is taken from the seed - so what is not in the seed cannot start a deployment, and a
     * declaration that changed would silently never reach the database. A hash and not the content
     * itself: the seed answers 'has this changed', and the content is deployed by the script that
     * owns it.
     *
     * Its fillers are the rows a file declares: the values of a set, the numberings of /autonum,
     * the rows of a seed file. A table holding none has none, and a kind is never asked - what a
     * table declares is what it has.
     */
    internal static String? Xtra(this TableMetadata table)
    {
        // a seed value that is not named and one named null are different rows to the merge
        static String SeedLine(SeedRow row) =>
            $"{row.Id}|{String.Join('|', row.Values.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Value == null ? $"{kv.Key}~" : $"{kv.Key}={kv.Value}"))}";

        // the position is part of a value (it becomes Order), so the index is in the text; a
        // numbering is addressed by its key alone and its position says nothing
        // every field of a value, unconditionally: appended only when set, a colour and a role
        // would produce one text for two different rows
        var lines = table.Values
            .Select((v, ix) => $"{ix}|{v.Id}|{v.Name}|{v.Memo}|{(v.Void ? 1 : 0)}|{v.Color}|{v.Role}")
            .Concat(table.Autonums.Select(a => $"{a.Id}|{a.Name}|{a.Pattern}|{a.Period}"))
            .Concat(table.Operations.Select(o => o.Id))
            .Concat(table.SeedRows.Select(SeedLine))
            .ToList();
        if (lines.Count == 0)
            return null;
        var text = String.Join('\n', lines);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    /* The names the transactions dialog calls one journal by - its array, its row type, the value
     * its tab switches on and the key its caption is localized under. All four are the journal's
     * own NAME, and they are one function because a drift between them is a tab that matches no
     * case.
     *
     * The name is the folder the journal is declared in - the second segment of its address, which
     * is what the platform's grammar calls a thing and what TableMetadata.SetDefaults itself
     * PascalCases into the default Model. Neither of the two stored spellings answers this:
     * 'Model' is a shape and several journals may share one ('Transaction' would name two different
     * arrays, of two different column sets, in one model); 'Table' is where the rows are stored,
     * which renames only through a migration and reads as 'jrn.StockJournal', not as the journal.
     *
     * Unique because a folder is: PostJournals is distinct by table, and one schema has one 'stock'.
     */
    internal static String TransName(this TableMetadata journal) =>
        journal.Path.Split('/')[^1].ToPascalCase();
    internal static String TransTypeName(this TableMetadata journal) => $"T{journal.TransName()}";

    internal static IEnumerable<RefDescriptor> AllRefs(this IEnumerable<TableColumn> columns) =>
        columns.Where(c => c.IsRef || c.IsOperation).Select((c, ix) => new RefDescriptor(ix + 1, c, (c.RefTable
            ?? throw new InvalidOperationException($"RefTable for {c.Name} is null")).Storage));

    /* What a NEW record of this endpoint starts on, with every far half resolved: what the file
     * declared (its initialValues and its fixed fields, already merged by the bake), the operation
     * an endpoint over a shared document storage IS, and the initial state of every state column.
     *
     * Not folded into the bake, and the line is the one the inherit rules are already cut along:
     * the bake runs before the reference graph is linked, and the last two are the FAR half - a
     * code the SET declares, a row of a registry. Asked here instead, where the SQL is written and
     * the graph is complete.
     *
     * One answer for the two readers that need it - the defaults recordset, which sends the values,
     * and the map, which resolves the references among them into objects. They were two lists kept
     * equal by hand, and the operation appeared in both under two different spellings; a third
     * source would have made it six places.
     */
    internal static IReadOnlyDictionary<String, InitialMetadata> AllInitials(this NormalEndpointMetadata endpoint)
    {
        var initials = new Dictionary<String, InitialMetadata>(endpoint.Declaration.Initials);

        /* A literal like any other: the value is the code, and the column is found BY TYPE. The
         * name was spelled 'Operation' literally in both readers, so a document that called the
         * column anything else got a map insert into a column that is not there and a default for
         * a property the model does not carry.
         */
        if (endpoint.DocumentOperation() is { Length: > 0 } operation)
        {
            var column = endpoint.Storage.AllColumns().First(c => c.IsOperation);
            initials[column.Name] = new InitialMetadata(InitialSource.Literal, operation);
        }

        // the set says where its cycle begins; the endpoint may not say it again (DeclarationBake)
        foreach (var column in endpoint.Storage.AllColumns(c => c.Type == ColumnType.State))
        {
            var set = column.RefTableCheck.Storage;
            var initial = set.Values.FirstOrDefault(v => !v.Void && v.Role == StateRole.Initial)
                ?? throw new InvalidOperationException(
                    $"{set.Path}: no living state has the role '{StateRole.Initial}'");
            initials[column.Name] = new InitialMetadata(InitialSource.Literal, initial.Id);
        }
        return initials;
    }

    /* The operation is the endpoint, so its key is the endpoint name - not a slice of a path. Only an
     * endpoint over a storage declared elsewhere is one: the storage itself (/document, an empty name)
     * is the whole family, and answering '' for it filtered its list down to nothing.
     */
    internal static String? DocumentOperation(this NormalEndpointMetadata endpoint) =>
        !endpoint.Declaration.HasOwnShape && endpoint.Storage.IsDocument && endpoint.Storage.Columns.Any(c => c.IsOperation)
            ? endpoint.Name : null;
}

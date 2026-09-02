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
        => col.Type != ColumnType.RowVersion && col.Type != ColumnType.Void && col.Type != ColumnType.IsSystem;
    internal static Boolean IsEditColumn(TableColumn col)
        => col.Type != ColumnType.RowVersion && col.Type != ColumnType.Void && col.Type != ColumnType.IsSystem
            && col.Type != ColumnType.Id && col.Type != ColumnType.Done;
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
            Constants.SchemaNames.Autonum => EndpointKind.Autonum,
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
        table.DefaultColumns().Concat(table.Columns).Where(predicate ?? (_ => true));

    /* Everything about a table that the seed cannot say in columns, as one fingerprint. The deploy
     * hash is taken from the seed - so what is not in the seed cannot start a deployment, and a
     * declaration that changed would silently never reach the database. A hash and not the content
     * itself: the seed answers 'has this changed', and the content is deployed by the script that
     * owns it.
     *
     * Its fillers are the rows a file declares: the values of an enum, the numberings of /autonum.
     * A table holding neither has none, and a kind is never asked - what a table declares is what
     * it has.
     */
    internal static String? Xtra(this TableMetadata table)
    {
        // the position is part of a value (it becomes Order), so the index is in the text; a
        // numbering is addressed by its key alone and its position says nothing
        var lines = table.Values
            .Select((v, ix) => $"{ix}|{v.Id}|{v.Name}|{v.Memo}|{(v.Void ? 1 : 0)}")
            .Concat(table.Autonums.Select(a => $"{a.Id}|{a.Name}|{a.Pattern}|{a.Period}"))
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

    // the operation is the endpoint, so its key is the endpoint name - not a slice of a path
    internal static String? DocumentOperation(this NormalEndpointMetadata endpoint) =>
        endpoint.Storage.IsDocument && endpoint.Storage.Columns.Any(c => c.IsOperation) ? endpoint.Name : null;
}

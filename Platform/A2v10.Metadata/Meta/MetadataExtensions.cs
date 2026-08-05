// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

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
            _ => throw new InvalidOperationException($"Invalid schema for EndpointKind {schema}")
        };
    }

    internal static String ToSqlSchema(this String folder)
    {
        return folder switch
        {
            Constants.SchemaNames.Catalog => "cat",
            Constants.SchemaNames.Document => "doc",
            Constants.SchemaNames.Journal => "jrn",
            "report" => "rep",
            Constants.SchemaNames.Operations => "op",
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


    internal static IEnumerable<ReportItemMetadata> TypedReportItems(this ReportMetadata table, ReportItemKind kind)
    {
        return table.ReportItems.Where(ri => ri.Kind == kind).OrderBy(r => r.Order);
    }

    internal static String Endpoint(this ReportItemMetadata item)
    {
        return $"/{item.RealRefSchema}/{item.RealRefTable}";
    }
    internal static String CreateField(this ReportItemMetadata item, String? prefix = null)
    {
        return $"[{prefix}{item.Column}] {item.DataType.ToSqlDataType()}";
    }

    internal static TableMetadata CreateEnumMeta(TableColumn col)
    {
        return new TableMetadata()
        {
            //Schema = col.Reference.RefSchema,
            Table = col.RefTableCheck.Storage.Table,
            /*
            Columns = [
                new TableColumn()
                    {
                        Name = "Id",
                        DataType = ColumnDataType.String,
                        MaxLength = 16,
                        Role = TableColumnRole.PrimaryKey,
                    },
                    new TableColumn()
                    {
                        Name = "Name",
                        DataType = ColumnDataType.String,
                        MaxLength = 255,
                        Role = TableColumnRole.Name,
                    }
            ]
            */
        };
    }

    internal static IEnumerable<TableColumn> AllColumns(this TableMetadata table, Func<TableColumn, Boolean>? predicate = null) =>
        table.DefaultColumns().Concat(table.Columns).Where(predicate ?? (_ => true));

    internal static IEnumerable<RefDescriptor> AllRefs(this IEnumerable<TableColumn> columns) =>
        columns.Where(c => c.IsRef || c.IsOperation).Select((c, ix) => new RefDescriptor(ix + 1, c, (c.RefTable
            ?? throw new InvalidOperationException($"RefTable for {c.Name} is null")).Storage));


    /* 'required' is a KIND of rule, so it is read from the declaration and from nowhere else.
     * The shape knows that a field exists; that completing the record takes it is a fact of the
     * layer that owns the moment of completion, and that layer is the declaration.
     *
     * The names are checked against the shape here, where the table is at hand: a misspelled
     * name would otherwise produce no validator at all, which is exactly the failure a missing
     * validator cannot be told apart from.
     *
     * Default columns count as fields: 'Name' and 'Date' are part of the record, they are only
     * not spelled in 'fields'.
     */
    internal static String[] RequiredFields(this TableMetadata table, DeclarationMetadata declaration)
    {
        var declared = declaration.Rules.Required;
        if (declared.Length == 0)
            return [];
        foreach (var name in declared)
            if (!table.AllColumns().Any(c => c.Name == name))
                throw new InvalidOperationException($"required: field '{name}' not found in {table.SqlTableName}");
        return declared;
    }

    /* 'inherit' is a KIND of rule, so it is read from the declaration and from nowhere else.
     * There is no second source to merge with: the endpoint's declaration already carries the
     * storage layer under its own (DatabaseMetadataProvider.MergeDeclaration), so a shape-side
     * copy would only re-read what is here.
     *
     * The declaration is required, not optional: 'no declaration' and 'a declaration with no
     * inherits' are the same answer, and a defaulted parameter would let a caller ask for the
     * first while meaning neither.
     */
    internal static IEnumerable<InheritDescriptor> AllInherits(this TableMetadata table, DeclarationMetadata declaration)
    {
        var declared = declaration.Rules.Inherit;
        if (declared.Count == 0)
            yield break;

        var columns = table.Columns; 

        static TableColumn Find(TableMetadata t, List<TableColumn> cols, String name, String what) =>
            cols.FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException($"inherit: {what} '{name}' not found in {t.SqlTableName}");

        foreach (var kp in declared)
        {
            var field = Find(table, columns, kp.Key, "field");
            var refColumn = Find(table, columns, kp.Value.Ref, "ref");
            if (!refColumn.IsRef)
                throw new InvalidOperationException($"inherit: ref '{refColumn.Name}' is not a reference");
            var refTable = refColumn.RefTableCheck.Storage;
            yield return new InheritDescriptor(field, refColumn,
                Find(refTable, refTable.Columns, kp.Value.Field, "source"));
        }
    }
    // root + details; each table paired with the declaration that speaks about it
    internal static IEnumerable<InheritDescriptor> AllInheritsDeep(this TableMetadata table, DeclarationMetadata declaration)
    {
        foreach (var d in table.AllInherits(declaration))
            yield return d;
        foreach (var (name, detail) in table.Details)
        {
            // a collection nobody declared rules for has no rules - not an empty layer to visit
            var declared = declaration.Details.GetValueOrDefault(name);
            if (declared == null)
                continue;
            foreach (var d in detail.AllInheritsDeep(declared))
                yield return d;
        }
    }

    /* Default forms are built on demand, not while the table is being constructed: a table that is
     * deployed but never rendered (tags) has no forms to build, and asking for them throws. A form
     * declared in the file is already in the dictionary and wins.
     */
    internal static FormMetadata IndexForm(this TableMetadata table) =>
        table.Forms.GetOrAdd(Constants.FormNames.Index,
            _ => DefaultFormBuilder.CreateIndexForm(table).SetDefaults(table, TableColumnPredicates.IsIndexColumn));
    internal static FormMetadata BrowseForm(this TableMetadata table) =>
        table.Forms.GetOrAdd(Constants.FormNames.Browse,
            _ => DefaultFormBuilder.CreateBrowseForm(table).SetDefaults(table, TableColumnPredicates.IsIndexColumn));
    internal static FormMetadata EditForm(this TableMetadata table) =>
        table.Forms.GetOrAdd(Constants.FormNames.Edit,
            _ => DefaultFormBuilder.CreateEditForm(table).SetDefaults(table, TableColumnPredicates.IsEditColumn));

    public static IEnumerable<TableColumn> TableFilters(this TableMetadata table)
    {
        if (table.HasPeriod)
            yield return new TableColumn("Date", ColumnType.Date);
        foreach (var c in table.AllColumns(c => c.IsRef))
            yield return c;
    }

    // the operation is the endpoint, so its key is the endpoint name - not a slice of a path
    internal static String? DocumentOperation(this NormalEndpointMetadata endpoint) =>
        endpoint.Storage.IsDocument && endpoint.Storage.Columns.Any(c => c.IsOperation) ? endpoint.Name : null;
}

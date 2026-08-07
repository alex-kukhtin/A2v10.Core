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
    private static void CheckNames(TableMetadata table, String[] names, String what)
    {
        foreach (var name in names)
            if (!table.AllColumns().Any(c => c.Name == name))
                throw new InvalidOperationException($"{what}: field '{name}' not found in {table.SqlTableName}");
    }

    /* 'inherit' is a KIND of rule, so it is read from the rules and from nowhere else. There is
     * no second source to merge with: what arrives here is already layered - storage under
     * operation by MergeDeclaration, collection under row kind by RulesFor - so a shape-side
     * copy would only re-read what is here.
     *
     * The one place a name is turned into a column, and the only place that needs the table for
     * it. Private on purpose: every reader takes the answer off the declaration, and a second
     * caller would be a second moment at which 'what is in force' could be decided.
     */
    private static Dictionary<String, InheritDescriptor[]> BuildInherits(TableMetadata table, RuleMetadata rules)
    {
        var declared = rules.Inherit;
        if (declared.Count == 0)
            return [];

        var columns = table.Columns;

        static TableColumn Find(TableMetadata t, List<TableColumn> cols, String name, String what) =>
            cols.FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException($"inherit: {what} '{name}' not found in {t.SqlTableName}");

        IEnumerable<InheritDescriptor> Resolve()
        {
            foreach (var kp in declared)
            {
                var field = Find(table, columns, kp.Key, "field");
                var refColumn = Find(table, columns, kp.Value.Ref, "ref");
                if (!refColumn.IsRef)
                    throw new InvalidOperationException($"inherit: ref '{refColumn.Name}' is not a reference");
                yield return new InheritDescriptor(field, refColumn, kp.Value.Field);
            }
        }

        return Resolve().GroupBy(d => d.Ref.Name).ToDictionary(g => g.Key, g => g.ToArray());
    }

    /* The other half of InheritDescriptor's asymmetry, asked once the reference graph is linked.
     * Late, and therefore the one name of the three that a typo carries past load - so it says
     * where it looked, in the same words the other two do.
     */
    internal static TableColumn SourceColumn(this InheritDescriptor descriptor)
    {
        var refTable = descriptor.Ref.RefTableCheck.Storage;
        return refTable.Columns.FirstOrDefault(c => c.Name == descriptor.Source)
            ?? throw new InvalidOperationException(
                $"inherit: source '{descriptor.Source}' not found in {refTable.SqlTableName}");
    }

    /* Names the file wrote that the shape has no counterpart for. Silently skipping them is what
     * made a typo in a collection or kind key produce an endpoint that simply generated less.
     */
    private static void NoLeftovers(TableMetadata table, IEnumerable<String> declared,
        IEnumerable<String> shape, String what)
    {
        var extra = declared.Except(shape).ToList();
        if (extra.Count == 0)
            return;
        var available = String.Join(", ", shape);
        throw new InvalidOperationException(
            $"{what}: [{String.Join(", ", extra)}] declared for {table.SqlTableName}, which has no such {what}. "
            + (available.Length == 0 ? $"It declares no {what} at all." : $"Available: {available}"));
    }

    /* The one walk that pairs a declaration node with the shape node it speaks about. Run while
     * the endpoint is being constructed, which is possible only because nothing here reaches
     * outside its own table - see InheritDescriptor. That is what keeps the endpoint immutable:
     * a bake that needed the reference graph would have to run after publication, and there is
     * no way to put a new declaration into a record everyone already points at.
     *
     * Rebuilds rather than fills: an operation that declares no rows of its own gets the storage
     * endpoint's detail nodes by reference (MergeDetails), and writing into what was found would
     * be writing into another endpoint's declaration. With 'with' there is nothing to write into.
     *
     * Driven from the SHAPE, which is what makes the result total: every collection of the table
     * gets a node and every row set gets an entry, whether the file mentioned them or not. That
     * is the difference between 'nothing was declared' and 'nothing is declared', and collapsing
     * the two is what deletes the 'if (declared == null)' every generator used to carry.
     */
    internal static DeclarationMetadata Bake(this DeclarationMetadata declaration, TableMetadata table)
    {
        CheckNames(table, declaration.Rules.Required, "required");
        if (declaration.Rules.Total.Length > 0)
            throw new InvalidOperationException(
                $"total: declared on {table.SqlTableName}, which is a record. A sum is a member of a collection.");
        NoLeftovers(table, declaration.Kinds.Keys, [], "kinds");
        return declaration.BakeNode(table);
    }

    private static DeclarationMetadata BakeNode(this DeclarationMetadata declaration, TableMetadata table)
    {
        NoLeftovers(table, declaration.Details.Keys, table.Details.Keys, "details");
        return declaration with
        {
            Inherits = BuildInherits(table, declaration.Rules),
            Details = table.Details.ToDictionary(
                kp => kp.Key,
                kp => (declaration.Details.GetValueOrDefault(kp.Key) ?? new()).BakeCollection(kp.Value))
        };
    }

    private static DeclarationMetadata BakeCollection(this DeclarationMetadata declaration, TableMetadata table)
    {
        NoLeftovers(table, declaration.Kinds.Keys, table.Kinds.Keys, "kinds");
        return declaration.BakeNode(table) with
        {
            RowSets = [.. table.RowSets().Select(rs =>
            {
                var rules = declaration.RulesFor(rs.Kind);
                CheckNames(table, rules.Required, "required");
                CheckNames(table, rules.Total, "total");
                return new RowSetDeclaration(rs.Kind, rs.Collection, rs.Type, rules,
                    BuildInherits(table, rules));
            })]
        };
    }

    /* Every inherit of an endpoint, root and rows alike. The consumer is the ref-map closure -
     * which columns a picked object has to carry - and there the union over kinds is what is
     * wanted, because the slot has to exist in the type of whichever kind declared it.
     *
     * A kind that declared nothing contributes the collection's, which its row set already
     * carries; the duplicates that produces are the caller's to fold, and it does.
     */
    internal static IEnumerable<InheritDescriptor> AllInherits(this DeclarationMetadata declaration)
    {
        foreach (var d in declaration.Inherits.Values.SelectMany(x => x))
            yield return d;
        foreach (var rowSet in declaration.RowSets)
            foreach (var d in rowSet.Inherits.Values.SelectMany(x => x))
                yield return d;
        foreach (var detail in declaration.Details.Values)
            foreach (var d in detail.AllInherits())
                yield return d;
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

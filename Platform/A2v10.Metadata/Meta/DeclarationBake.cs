// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* The pass that pairs a declaration with the shape it speaks about, run once while the endpoint is
 * built - before publication, reporting by throwing. See CLAUDE.md, "Declarations".
 *
 * A pass rather than a method of DeclarationMetadata because it walks TWO types at once and its
 * checkers are shared by both levels of that walk; FormMetadata.Bake is an instance method for the
 * mirror reason, its walk never leaves its own type family. Readers that descend the same levels
 * the pass fills live here too - AllInherits is the one.
 */
internal static class DeclarationBake
{
    /* Checked here, where the table is at hand: a misspelled name would otherwise produce no
     * validator at all, which is exactly the failure a missing validator cannot be told apart from.
     * Default columns count as fields - 'Name' and 'Date' are part of the record, they are only not
     * spelled in 'fields'.
     */
    private static void CheckNames(TableMetadata table, String[] names, String what)
    {
        foreach (var name in names)
            if (!table.AllColumns().Any(c => c.Name == name))
                throw new InvalidOperationException($"{what}: field '{name}' not found in {table.SqlTableName}");
    }

    /* The one place a name is turned into a column, and the only place that needs the table for it.
     * What arrives is already layered - by MergeDeclaration and RulesFor - so there is no second
     * source to merge with here. Private on purpose: a second caller would be a second moment at
     * which 'what is in force' could be decided.
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

    /* The root of the walk. It can run this early only because nothing here reaches outside its own
     * table - see InheritDescriptor - and that is what keeps the endpoint immutable: a bake needing
     * the reference graph would have to run after publication, and there is no way to put a new
     * declaration into a record everyone already points at.
     */
    internal static DeclarationMetadata Bake(this DeclarationMetadata declaration, TableMetadata table)
    {
        CheckNames(table, declaration.Rules.Required, "required");
        if (declaration.Rules.Total.Length > 0)
            throw new InvalidOperationException(
                $"total: declared on {table.SqlTableName}, which is a record. A sum is a member of a collection.");
        NoLeftovers(table, declaration.Kinds.Keys, [], "kinds");
        return declaration.BakeNode(table) with { BakedForms = BuildForms(declaration, table) };
    }

    /* Which shapes have forms at all - a table is deployed whether or not anything renders it, and
     * DefaultFormBuilder knows the command bar of the rendered kinds only.
     *
     * Asked of the TABLE, not of the endpoint: EndpointKindOf resolves the platform namespaces
     * ('operations', 'tag') to Undefined, so the kind that can answer this is the shape's.
     */
    private static Boolean HasForms(this TableMetadata table) =>
        table.Kind is EndpointKind.Catalog or EndpointKind.Document
            or EndpointKind.Journal or EndpointKind.Operation;

    /* Every form of the endpoint - declared or default, resolved against the shape either way, and
     * total: for a shape that renders, all three keys are present. See CLAUDE.md, "Forms: whole or
     * nothing".
     */
    private static IReadOnlyDictionary<String, FormMetadata> BuildForms(DeclarationMetadata declaration,
        TableMetadata table)
    {
        String[] names = [Constants.FormNames.Index, Constants.FormNames.Browse, Constants.FormNames.Edit];
        NoLeftovers(table, declaration.Forms.Keys, names, "forms");

        if (!table.HasForms())
            return new Dictionary<String, FormMetadata>();

        // which form, on the way out: three are built in one breath, and nothing deeper knows which
        FormMetadata Build(String name, Func<TableMetadata, FormMetadata> createDefault,
            Func<TableMetadata, List<MemberDescriptor>> candidates)
        {
            try
            {
                return (declaration.Forms.GetValueOrDefault(name) ?? createDefault(table))
                    .Bake(table, candidates(table));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"form '{name}': {ex.Message}", ex);
            }
        }

        /* The one place that says which form sees what: the index forms show columns, the edit form
         * shows columns plus whatever a trait contributes. See CLAUDE.md, "Members".
         */
        return new Dictionary<String, FormMetadata>()
        {
            { Constants.FormNames.Index,
                Build(Constants.FormNames.Index, DefaultFormBuilder.CreateIndexForm, MemberMetadata.IndexMembers) },
            { Constants.FormNames.Browse,
                Build(Constants.FormNames.Browse, DefaultFormBuilder.CreateBrowseForm, MemberMetadata.IndexMembers) },
            { Constants.FormNames.Edit,
                Build(Constants.FormNames.Edit, t => DefaultFormBuilder.CreateEditForm(t, declaration),
                    MemberMetadata.EditMembers) }
        };
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
        // symmetric to 'total' on the root: 'details' holds the very same record type, so a key it
        // has no answer for would deserialize and then vanish
        if (declaration.Forms.Count > 0)
            throw new InvalidOperationException(
                $"forms: declared on '{table.DetailsKey}', which is a collection. A form belongs to the "
                + "endpoint that shows it and reaches the rows through 'scope'.");
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
     * wanted, because the slot has to exist in the type of whichever kind declared it. A kind that
     * declared nothing contributes the collection's; the duplicates are the caller's to fold.
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
}

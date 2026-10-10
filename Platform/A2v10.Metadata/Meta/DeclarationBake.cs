// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
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

        // Default columns count, as in CheckNames: 'Memo' and 'Folder' are the record's too
        var columns = table.AllColumns().ToList();

        static TableColumn Find(TableMetadata t, IReadOnlyList<TableColumn> cols, String name, String what) =>
            cols.FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException($"inherit: {what} '{name}' not found in {t.SqlTableName}");

        IEnumerable<InheritDescriptor> Resolve()
        {
            foreach (var kp in declared)
            {
                var field = Find(table, columns, kp.Key, "field");
                if (field.HasSqlAs)
                    throw new InvalidOperationException(
                        $"inherit: field '{field.Name}' has 'sqlAs' - its value is the database's, a snapshot has nowhere to land");
                var refColumn = Find(table, columns, kp.Value.Ref, "ref");
                if (!refColumn.IsRef)
                    throw new InvalidOperationException($"inherit: ref '{refColumn.Name}' is not a reference");
                yield return new InheritDescriptor(field, refColumn, kp.Value.Field);
            }
        }

        return Resolve().GroupBy(d => d.Ref.Name).ToDictionary(g => g.Key, g => g.ToArray());
    }

    /* A property over a field stands in for its column, so the field must be one, and its type is
     * the one in 'fields'. A '$' name has no column to take a type from, and the form takes format
     * and control from the type, so it is written here - a value the client holds, never a
     * reference: a reference is drawn through the map, and a name nothing loads has none.
     *
     * 'inherit' on the same field is refused: inherited, the value is the record's snapshot; under a
     * property it is the getter's. One field, one source of value.
     */
    private static void CheckProperties(TableMetadata table, Dictionary<String, PropertyMetadata> properties, RuleMetadata rules)
    {
        foreach (var (key, p) in properties)
        {
            if (String.IsNullOrEmpty(p.Get))
                throw new InvalidOperationException($"properties: '{key}' declares no 'get'");
            if (PropertyMetadata.IsOwnName(key))
            {
                if (p.Type is not { } type)
                    throw new InvalidOperationException(
                        $"properties: '{key}' has no column, so 'type' says what it is - the form takes its format and control from it");
                if (TableColumn.IsRefType(type))
                    throw new InvalidOperationException(
                        $"properties: '{key}' is of type '{type}'. A '$' name is a value the client holds; a reference is drawn through the map, and nothing loads one for it");
                continue;
            }
            if (!table.AllColumns().Any(c => c.Name == key))
                throw new InvalidOperationException(
                    $"properties: field '{key}' not found in {table.SqlTableName}. A name of its own, with no column, starts with '$'");
            if (p.Type != null)
                throw new InvalidOperationException(
                    $"properties: '{key}' declares 'type'. It is a field, and its type is the one in 'fields'");
            if (rules.Inherit.ContainsKey(key))
                throw new InvalidOperationException(
                    $"properties: '{key}' is also in 'inherit'. Inherited, the value is the record's; under a property it is the getter's - one field, one source of value");
        }
    }

    /* A condition holds the same rules as the block it stands in, and they are checked the same way.
     * 'inherit' and 'total' under one are refused rather than dropped: no generator writes them, and
     * 'inherit' also lands in the SQL of a birth, where a client test cannot run.
     */
    private static void CheckWhen(TableMetadata table, RuleMetadata rules)
    {
        foreach (var w in rules.When)
        {
            if (String.IsNullOrWhiteSpace(w.Test))
                throw new InvalidOperationException("when: an entry declares no 'test'");
            CheckNames(table, w.Required, $"when '{w.Test}': required");
            if (w.Inherit.Count > 0)
                throw new InvalidOperationException(
                    $"when '{w.Test}': declares 'inherit'. An inherit under a condition is not generated yet; declare it unconditionally.");
            if (w.Total.Length > 0)
                throw new InvalidOperationException(
                    $"when '{w.Test}': declares 'total'. A total under a condition is not generated yet; declare it unconditionally.");
        }
    }

    /* The rules an operation declares are 'when' under a test of the operation column: one type
     * serves every operation of the document, so the operation in force is a value the client holds.
     * The test is derived, never written - the code is the platform's - and it reads the column
     * through $root, so one spelling serves the header and the rows. A 'when' of the operation's own
     * holds under both tests.
     *
     * The column is there for certain: a document listing operations without it is refused before
     * the bake (DatabaseMetadataProvider.CheckOperationColumn).
     */
    private static DeclarationMetadata WithOperationRules(this DeclarationMetadata declaration, TableMetadata table)
    {
        if (declaration.OperationDeclarations.Count == 0)
            return declaration;
        var column = table.AllColumns().First(c => c.IsOperation);

        static RuleMetadata Under(RuleMetadata into, RuleMetadata rules, String test)
        {
            IEnumerable<ConditionalRuleMetadata> conditional()
            {
                if (rules.Required.Length > 0 || rules.Total.Length > 0 || rules.Visible.Count > 0 || rules.Inherit.Count > 0)
                    yield return new()
                    {
                        Test = test,
                        Required = rules.Required,
                        Total = rules.Total,
                        Visible = rules.Visible,
                        Inherit = rules.Inherit
                    };
                foreach (var w in rules.When)
                    yield return w with { Test = $"({test}) && ({w.Test})" };
            }
            return into with { When = [.. into.When, .. conditional()] };
        }

        static DeclarationMetadata Collection(DeclarationMetadata into, DeclarationMetadata rules, String test)
        {
            var kinds = new Dictionary<String, KindDeclarationMetadata>(into.Kinds);
            foreach (var (name, kind) in rules.Kinds)
            {
                var target = kinds.GetValueOrDefault(name) ?? new();
                kinds[name] = target with { Rules = Under(target.Rules, kind.Rules, test) };
            }
            return into with { Rules = Under(into.Rules, rules.Rules, test), Kinds = kinds };
        }

        var result = declaration;
        foreach (var operation in declaration.OperationDeclarations)
        {
            var test = $"this.$root.{table.Model}.{column.Name}.Id === '{operation.Id}'";
            var details = new Dictionary<String, DeclarationMetadata>(result.Details);
            foreach (var (name, rules) in operation.Details)
                details[name] = Collection(details.GetValueOrDefault(name) ?? new(), rules, test);
            result = result with { Rules = Under(result.Rules, operation.Rules, test), Details = details };
        }
        return result;
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
    internal static DeclarationMetadata Bake(this DeclarationMetadata declaration, TableMetadata table, AppPlatformId platformId)
    {
        declaration = declaration.WithOperationRules(table);
        CheckNames(table, declaration.Rules.Required, "required");
        CheckWhen(table, declaration.Rules);
        if (declaration.Rules.Total.Length > 0)
            throw new InvalidOperationException(
                $"total: declared on {table.SqlTableName}, which is a record. A sum is a member of a collection.");
        CheckProperties(table, declaration.Properties, declaration.Rules);
        NoLeftovers(table, declaration.Kinds.Keys, [], "kinds");
        return declaration.BakeNode(table) with
        {
            BakedForms = BuildForms(declaration, table, platformId),
            Initials = BuildInitials(declaration, table)
        };
    }

    /* A fixed value as SQL will spell it, by the column and never by the look of the text - the
     * same rule a literal initial follows (SqlExtensions.SqlLiteral). JSON hands over a Boolean, a
     * number or a string; a reference key is refused there, as it is for a literal initial.
     */
    internal static String FixedText(Object value) => value switch
    {
        Boolean b => b ? "1" : "0",
        null => throw new InvalidOperationException("fixed: a value is null"),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!
    };

    /* The initials of a new record: what the file declared, and the fixed fields on top as literals.
     * Checked here, where the table is at hand: a fixed field must be a column, its value must be
     * spellable for that column, and a field cannot be both fixed and given an initial - two
     * answers to what a new row starts on.
     */
    private static IReadOnlyDictionary<String, InitialMetadata> BuildInitials(DeclarationMetadata declaration, TableMetadata table)
    {
        var initials = new Dictionary<String, InitialMetadata>(declaration.InitialValues);

        /* Asked here and not where literal initials are checked against the codes, because there
         * the two keys have already been merged into one map and the message would name the key
         * the author did not write. The type is enough to ask it this early: whether a column is a
         * state is on the column, and only the CODE it starts on lives in the far half.
         */
        foreach (var (name, initial) in declaration.InitialValues)
        {
            // a name the shape has not: unrefused, it failed at the first 'edit/new', and 'context' never even there
            var column = table.AllColumns().FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException($"initialValues: field '{name}' not found in {table.SqlTableName}");
            if (column.Type == ColumnType.State)
                throw new InvalidOperationException(
                    $"initialValues: '{name}' is a state column. Where a new record starts is the SET's own answer - its value with role '{StateRole.Initial}' - and a second spelling of one fact is free to disagree with it.");
            if (column.HasSqlAs)
                throw new InvalidOperationException(
                    $"initialValues: '{name}' has 'sqlAs' - its value is the database's, a new record does not start on one.");
            if (initial.Source == InitialSource.Context && initial.Value != Constants.ContextValues.Today)
                throw new InvalidOperationException(
                    $"initialValues: '{name}' - '{initial.Value}' is not a value of the context. Known: {Constants.ContextValues.Today}");
        }

        foreach (var (name, value) in declaration.Fixed)
        {
            var column = table.AllColumns().FirstOrDefault(c => c.Name == name)
                ?? throw new InvalidOperationException($"fixed: field '{name}' not found in {table.SqlTableName}");
            if (column.Type == ColumnType.State)
                throw new InvalidOperationException(
                    $"fixed: '{name}' is a state column. A fixed field IS the initial of a new record, and a state's initial is the set's - its value with role '{StateRole.Initial}'. An address about some of the states is a filter, not a fixed field.");
            if (column.HasSqlAs)
                throw new InvalidOperationException(
                    $"fixed: '{name}' has 'sqlAs'. A fixed field IS the initial of a new record, and nothing writes one the database computes.");
            var text = FixedText(value);
            column.SqlLiteral(text); // throws for a column no literal can address
            if (initials.ContainsKey(name))
                throw new InvalidOperationException(
                    $"fixed: '{name}' is also in 'initialValues'. A fixed field IS the initial of a new record; keep one.");
            initials[name] = new InitialMetadata(InitialSource.Literal, text);
        }
        return initials;
    }

    /* Which shapes have forms at all - a table is deployed whether or not anything renders it, and
     * the template knows the standard command bar of the rendered kinds only.
     *
     * Asked of the TABLE: an endpoint has a type and no kind, and the registry of operations is a
     * table of this kind served by an endpoint of its own.
     */
    private static Boolean HasForms(this TableMetadata table) =>
        table.Kind is TableKind.Catalog or TableKind.Document
            or TableKind.Journal or TableKind.Operation or TableKind.AccPlan or TableKind.Ledger;

    /* Every form of the endpoint - declared or default, resolved against the shape either way, and
     * total: for a shape that renders, all three keys are present. See CLAUDE.md, "Forms: whole or
     * nothing".
     */
    private static IReadOnlyDictionary<String, FormMetadata> BuildForms(DeclarationMetadata declaration,
        TableMetadata table, AppPlatformId platformId)
    {
        String[] names = [Constants.FormNames.Index, Constants.FormNames.Browse, Constants.FormNames.Edit];
        NoLeftovers(table, declaration.Forms.Keys, names, "forms");

        if (!table.HasForms())
            return new Dictionary<String, FormMetadata>();

        // the edit dialog has no bar - its commands are the buttons at its foot - so a slot there never renders
        if (declaration.Forms.GetValueOrDefault(Constants.FormNames.Edit) is { Is: FormKind.Dialog, Toolbar.Commands.Count: > 0 })
            throw new InvalidOperationException(
                $"form '{Constants.FormNames.Edit}': 'toolbar' declared on a dialog, which has no command bar.");

        // which form, on the way out: three are built in one breath, and nothing deeper knows which
        FormMetadata Build(String name, Func<TableMetadata, FormMetadata> createDefault,
            Func<TableMetadata, List<MemberDescriptor>> candidates, RowCandidates rows)
        {
            try
            {
                return (declaration.Forms.GetValueOrDefault(name) ?? createDefault(table))
                    .Bake(table, candidates(table), [.. table.Filters(declaration)], rows);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"form '{name}': {ex.Message}", ex);
            }
        }

        static List<MemberDescriptor> plainRows(String scope, TableMetadata rows, String? kind) => rows.RowMembers();

        List<MemberDescriptor> editRows(String scope, TableMetadata rows, String? kind) =>
            rows.RowMembers().WithProperties(declaration.Details.GetValueOrDefault(scope)?.PropertiesFor(kind) ?? []);

        /* The one place that says which form sees what: the index forms show columns, the edit form
         * shows columns plus whatever a trait contributes, plus the declared properties - the edit
         * template is the one that carries them. See CLAUDE.md, "Members".
         */
        return new Dictionary<String, FormMetadata>()
        {
            { Constants.FormNames.Index,
                Build(Constants.FormNames.Index, t => DefaultFormBuilder.CreateIndexForm(t, declaration, platformId),
                    MemberMetadata.IndexMembers, plainRows) },
            { Constants.FormNames.Browse,
                Build(Constants.FormNames.Browse, t => DefaultFormBuilder.CreateBrowseForm(t, declaration, platformId),
                    MemberMetadata.IndexMembers, plainRows) },
            { Constants.FormNames.Edit,
                Build(Constants.FormNames.Edit, t => DefaultFormBuilder.CreateEditForm(t, switchesOperation: declaration.Operations.Count > 0),
                    t => t.EditMembers().WithProperties(declaration.Properties), editRows) }
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
                var properties = declaration.PropertiesFor(rs.Kind);
                CheckNames(table, rules.Required, "required");
                CheckNames(table, rules.Total, "total");
                CheckWhen(table, rules);
                CheckProperties(table, properties, rules);
                return new RowSetDeclaration(rs.Kind, rs.Collection, rs.Type, rules, properties,
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

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Core.Extensions;
using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Metadata;

/* The fetch behind a print blank: the 'Model' tree of the blank, resolved against the shape and
 * turned into SQL. See PRINT_FORMS_PLAN.md and CLAUDE.md, "Commands".
 *
 * Depth is done with RefId + chained Map recordsets, NOT with dotted names: a dotted property name
 * carries exactly ONE type name (FieldInfo, x[1]) and the loader refuses a third segment outright
 * (DataModelReader.ProcessComplexMetadata), so 'Agent.TaxDepartment.Name' has no spelling there. A
 * Map row goes through ProcessFields like any other, so a RefId INSIDE one creates the next
 * placeholder and the next Map resolves it - depth falls out of the mechanism rather than being a
 * feature, and the order of the recordsets does not matter (RefMapper keeps a forward definition
 * when a Map row arrives before its placeholder). Dedup comes free: one agent behind fifty rows is
 * fetched once.
 *
 * Nothing here holds an IDbContext or an ICurrentUser: it turns metadata into text and that is all,
 * so it is assertable without DI - TESTS.md 4.2, met by being born on the right side of it.
 */
internal sealed class PrintSqlBuilder(TableMetadata table, PrintNode model)
{
    /* A recordset of our OWN tree - the record, or a collection at any depth - and the predicate
     * that selects its rows. There are only two kinds of them, and a Map is not one: it is reached
     * by identity rather than by ownership, and nothing hangs off it.
     *
     * 'TypeName' is carried rather than taken from the table: a kinded collection addressed by its
     * composed name is the same table under a different type.
     */
    private sealed record Container(
        String Name, TableMetadata Table, String TypeName, PrintNode Node,
        String Rows, String Alias, Container? Parent);

    /* One referenced table as ONE path reached it: which rows (a select yielding ids) and what that
     * path asked of it. Paths onto one table are grouped before emitting.
     */
    private sealed record RefUse(TableMetadata Target, String IdSource, PrintNode Node);

    private sealed record MapTarget(TableMetadata Target, PrintNode Node, String Rows, String Alias);

    /* A collection as the blank may address it: the whole detail by its key, or one row kind by its
     * composed name. Both are the same table; the kind adds a type and a filter.
     */
    private sealed record Collection(TableMetadata Table, String TypeName, String? Kind)
    {
        public String Filter(String alias) => Kind == null
            ? String.Empty
            : $" and {alias}.[{Table.RowKindField}] = N'{Kind}'";
    }

    /* Every recordset gets an alias of its own and every column in every predicate is qualified
     * with one. Not decoration: a predicate is reused verbatim inside the subquery of whatever
     * hangs below it, and an unqualified name there binds to the INNERMOST scope that has such a
     * column - so a column the inner table happens to lack turns into a correlated reference to
     * the outer one. That is a silent wrong answer, not an error.
     */
    private Int32 _alias;
    private String NextAlias() => $"t{_alias++}";

    /* A map's alias is fixed to its TABLE, not handed out per round: CollectMaps regroups from
     * scratch until nothing changes, and an alias that differed each round would make the
     * comparison never come out equal.
     */
    private readonly Dictionary<String, String> _mapAliases = [];
    private String MapAlias(TableMetadata target)
    {
        if (!_mapAliases.TryGetValue(target.SqlTableName, out var alias))
            _mapAliases[target.SqlTableName] = alias = NextAlias();
        return alias;
    }

    private static String Id => Constants.FieldNames.Id;
    private static String Owner => Constants.FieldNames.Owner;

    /* 'TAgent', not 'TRAgent'. The 'TR' form belongs to the index, where a reference is resolved to
     * a stub of Id and Name and is a narrower thing than the record; here a map carries whatever the
     * blank asked of it, so it is the type - the same choice the plain model makes.
     */
    private static String TypeOfRef(TableMetadata t) => t.TypeName;

    public String Build()
    {
        if (model.IsCollection)
            throw new InvalidOperationException("print model: the root is a record, not a collection");
        if (model.Name != table.Model)
            throw new InvalidOperationException(
                $"print model: root is '{model.Name}', but {table.SqlTableName} is '{table.Model}'");

        /* Resolved in full before a line of SQL exists. Every name the blank got wrong fails the
         * build, not the render - and never the database, which is not touched here at all.
         */
        var containers = Containers().ToList();
        var maps = CollectMaps(containers);

        var sb = new StringBuilder($"""
            -- print model for {table.Model}

            set nocount on;
            set transaction isolation level read uncommitted;

            """);
        sb.AppendLine();

        WriteRecord(sb, containers[0]);
        foreach (var c in containers.Skip(1))
            WriteCollection(sb, c);
        foreach (var m in maps)
            WriteMap(sb, m);

        return sb.ToString();
    }

    // ---- the containers -------------------------------------------------------------------

    /* The record first, then every collection under it, at any depth, in the order they are
     * written. Recursion is on the WRITTEN tree, which is finite, so it ends by itself.
     */
    private IEnumerable<Container> Containers()
    {
        var alias = NextAlias();
        var root = new Container(
            model.Name, table, table.TypeName, model, $"{alias}.[{Id}] = @Id", alias, null);
        yield return root;
        foreach (var c in Nested(root))
            yield return c;
    }

    private IEnumerable<Container> Nested(Container owner)
    {
        foreach (var node in owner.Node.Nodes.Where(n => n.IsCollection))
        {
            var collection = CollectionOf(owner.Table, node.Name);
            var alias = NextAlias();

            /* Composed, with no special case for the first level. 'Owner = @Id' would be true of a
             * detail of the record and false of a detail of a detail: one law written twice, and
             * the second spelling wrong. Depth 1 pays one subquery for that.
             *
             * The parent's own predicate carries its kind filter, so rows of 'StockRows.SubRows'
             * come from the stock rows only - which is the whole point of addressing a kind.
             */
            var rows = $"{alias}.[{Owner}] in ({Ids(owner)}){collection.Filter(alias)}";

            var container = new Container(
                node.Name, collection.Table, collection.TypeName, node, rows, alias, owner);
            yield return container;
            foreach (var c in Nested(container))
                yield return c;
        }
    }

    // the ids of a container's own rows, as a subquery - the one thing anything below it needs
    private static String Ids(Container c) =>
        $"select {c.Alias}.[{Id}] from {c.Table.SqlTableName} {c.Alias} where {c.Rows}";

    // ---- the maps -------------------------------------------------------------------------

    /* Run to a fixed point, and regrouped from scratch each round rather than walked in waves.
     *
     * Two DIFFERENT paths of a finite tree reach one table at different depths - 'Document.
     * StoreFrom' and 'Document.Agent.Store' are both stores - and a table is ONE map, so its rows
     * are the union over every path onto it. A walk that emits a table when it is first reached
     * drops whatever arrives later, silently: the map is short some rows and the cell comes out
     * empty. Recomputing is what makes 'later' impossible.
     *
     * It settles because each round only reaches one level deeper than the last and the written
     * tree has a bottom; when a round changes no map's rows, nothing deeper exists.
     */
    private List<MapTarget> CollectMaps(List<Container> containers)
    {
        IEnumerable<RefUse> seed() =>
            containers.SelectMany(c => Refs(c.Table, c.Node, c.Rows, c.Alias));

        var maps = GroupMaps(seed());
        while (true)
        {
            var next = GroupMaps(
                [.. seed(), .. maps.SelectMany(m => Refs(m.Target, m.Node, m.Rows, m.Alias))]);
            if (Signature(next) == Signature(maps))
                return maps;
            maps = next;
        }
    }

    private static String Signature(List<MapTarget> maps) =>
        String.Join("|", maps.Select(m => $"{m.Target.SqlTableName}:{m.Rows}"));

    /* One map per TABLE, however many paths point at it: ids are the union of what those paths
     * select, fields the union of what they asked for. That is what makes the dedup real, and what
     * keeps one type name to one shape.
     */
    private List<MapTarget> GroupMaps(IEnumerable<RefUse> uses) =>
        [.. uses.GroupBy(r => r.Target.SqlTableName)
            .Select(g =>
            {
                var target = g.First().Target;
                var alias = MapAlias(target);
                return new MapTarget(
                    target,
                    Merge([.. g.Select(r => r.Node)]),
                    $"{alias}.[{Id}] in (\n    " +
                        String.Join("\n    union all\n    ", g.Select(r => r.IdSource).Distinct()) +
                        "\n  )",
                    alias);
            })];

    /* Every path onto one table as one demand. Fields union; nodes union by name, recursively -
     * two paths asking different things of the same agent still describe one agent.
     */
    private static PrintNode Merge(IReadOnlyList<PrintNode> nodes)
    {
        if (nodes.Count == 1)
            return nodes[0];
        return nodes[0] with
        {
            Fields = [.. nodes.SelectMany(n => n.Fields).Distinct()],
            Nodes = [.. nodes.SelectMany(n => n.Nodes).GroupBy(n => n.Name).Select(g => Merge([.. g]))]
        };
    }

    /* The references a container hands on, and which rows of the target they point at. Collected
     * apart from the text because a target's row set is the union over every path onto it, and that
     * is not known while any single path is being written.
     */
    private IEnumerable<RefUse> Refs(TableMetadata owner, PrintNode node, String rows, String alias)
    {
        foreach (var child in node.Nodes.Where(n => !n.IsCollection))
        {
            var column = RefColumn(owner, child.Name);
            yield return new RefUse(column.RefTableCheck.Storage,
                $"select {alias}.[{column.Name}] from {owner.SqlTableName} {alias} "
                    + $"where {rows} and {alias}.[{column.Name}] is not null",
                child);
        }
    }

    // ---- the text -------------------------------------------------------------------------

    private void WriteRecord(StringBuilder sb, Container c)
    {
        sb.AppendLine($"""
            select [{c.Name}!{c.TypeName}!Object] = null,
              {String.Join(", ", Fields(c.Table, c.Node, c.Alias, arrays: true))}
            from {c.Table.SqlTableName} {c.Alias} where {c.Rows};
            """);
        sb.AppendLine();
    }

    private void WriteCollection(StringBuilder sb, Container c)
    {
        var parent = c.Parent!;
        sb.AppendLine($"""
            -- {parent.Name}.{c.Name}
            select [!{c.TypeName}!Array] = null,
              {String.Join(", ", Fields(c.Table, c.Node, c.Alias, arrays: true))},
              [!{parent.TypeName}.{c.Name}!ParentId] = {c.Alias}.[{Owner}]
            from {c.Table.SqlTableName} {c.Alias} where {c.Rows}
            order by {c.Alias}.[{Constants.FieldNames.RowNo}];
            """);
        sb.AppendLine();
    }

    private void WriteMap(StringBuilder sb, MapTarget map)
    {
        sb.AppendLine($"""
            -- {map.Target.Model} map
            select [!{TypeOfRef(map.Target)}!Map] = null,
              {String.Join(", ", Fields(map.Target, map.Node, map.Alias, arrays: false))}
            from {map.Target.SqlTableName} {map.Alias} where {map.Rows};
            """);
        sb.AppendLine();
    }

    /* What one recordset sends. 'arrays' is what tells the two kinds of owner apart: a container
     * carries a slot per collection, a Map cannot - an array hanging off a Map object has no
     * spelling in the loader, and emitting one anyway yields a model that silently drops its rows.
     */
    private IEnumerable<String> Fields(TableMetadata owner, PrintNode node, String alias, Boolean arrays)
    {
        // Id is implicit: nothing is addressable without it, and no blank should have to say so
        yield return $"[{Id}!!{Id}] = {alias}.[{Id}]";

        foreach (var name in node.Fields)
        {
            if (name == Id)
                continue;
            var column = Column(owner, name);
            if (column.IsRef)
                throw new InvalidOperationException(
                    $"print model: '{name}' of {owner.SqlTableName} is a reference - write it as an object, not a field");
            yield return column.SqlModelColumnName(alias, TypeOfRef);
        }

        foreach (var child in node.Nodes)
        {
            if (child.IsCollection)
            {
                if (!arrays)
                    throw new InvalidOperationException(
                        $"print model: '{child.Name}[]' hangs off {owner.SqlTableName}, which is reached as a reference - a collection belongs to the record or to another collection");
                yield return $"[{child.Name}!{CollectionOf(owner, child.Name).TypeName}!Array] = null";
            }
            else
                yield return RefColumn(owner, child.Name).SqlModelColumnName(alias, TypeOfRef);
        }
    }

    // ---- where a written name meets the shape ----------------------------------------------

    /* A collection is addressable under BOTH names the platform gives it: the declared key of
     * 'details', which is every row of it, and the composed name of one row kind ('StockRows'),
     * which is that kind alone. A blank may use both at once - all the lines, and then the services
     * again by themselves - so this is not a choice to be made here.
     */
    private static Collection? FindCollection(TableMetadata owner, String name)
    {
        if (owner.Details.TryGetValue(name, out var whole))
            return new Collection(whole, whole.TypeName, null);

        foreach (var (_, detail) in owner.Details)
            foreach (var kind in detail.Kinds.Keys)
                if (detail.KindCollectionName(kind) == name)
                    return new Collection(detail, detail.KindTypeName(kind), kind);
        return null;
    }

    private static Collection CollectionOf(TableMetadata owner, String name) =>
        FindCollection(owner, name)
            ?? throw new InvalidOperationException(
                $"print model: '{name}[]' is not a collection of {owner.SqlTableName}");

    /* The ways a name can be wrong, each said with the table it was looked for in and in the same
     * words the inherit rules use. Nothing here guesses: a name is a column, a reference or a
     * collection, and which one was meant is written in the blank, not inferred.
     */
    private static TableColumn Column(TableMetadata owner, String name) =>
        owner.AllColumns().FirstOrDefault(c => c.Name == name)
            ?? throw new InvalidOperationException(
                $"print model: '{name}' not found in {owner.SqlTableName}");

    private static TableColumn RefColumn(TableMetadata owner, String name)
    {
        if (FindCollection(owner, name) != null)
            throw new InvalidOperationException(
                $"print model: '{name}' of {owner.SqlTableName} is a collection - write it as '{name}[]'");
        var column = Column(owner, name);
        return column.IsRef
            ? column
            : throw new InvalidOperationException(
                $"print model: '{name}' of {owner.SqlTableName} is not a reference - write it as a field, not an object");
    }
}

internal partial class SqlBuilder
{
    /* The IO edge: pick the blank, read it, parse it, hand the text over. Everything that can be
     * wrong about the blank is decided by PrintSqlBuilder, before the database is touched.
     */
    public async Task<IDataModel> LoadPrintModelAsync()
    {
        var forms = Endpoint.Declaration.PrintForms;
        if (forms.Count == 0)
            throw new InvalidOperationException($"print: {Endpoint.Path} declares no 'printForms'");

        var text = await ReadPrintFormAsync(PrintFormPath(forms));
        var sql = new PrintSqlBuilder(Table, PrintModel.Parse(text)).Build();

        return await _dbContext.LoadModelSqlAsync(_descr.DataSource, sql, dbprms =>
        {
            AddDefaultParameters(dbprms);
            dbprms.AddString("@Id", _descr.PlatformUrl.Id);
        });
    }

    /* '?form=' NAMES one of the blanks the endpoint declared - it is never a path from the client,
     * which would be a request for an arbitrary file. The name is the last segment of the declared
     * path, so 'print/printform1' answers to 'printform1'.
     *
     * REQUIRED, with no first-blank default. A print request that does not say what to print asks
     * for nothing, and answering it with whichever blank happens to be first hands back a
     * plausible wrong document instead of an error - the caller forgot a parameter and never
     * learns it. Same reason two blanks sharing a name is refused rather than won by the first.
     */
    private String PrintFormPath(List<PrintFormMetadata> forms)
    {
        var names = String.Join(", ", forms.Select(f => $"'{f.Name}'"));
        var asked = _descr.PlatformUrl.Query?.Get<String>(Constants.Print.FormQuery);
        if (String.IsNullOrEmpty(asked))
            throw new InvalidOperationException(
                $"print: {Endpoint.Path} was asked to print without '{Constants.Print.FormQuery}'. "
                    + $"It declares: {names}");

        var found = forms
            .Where(f => f.Name.Equals(asked, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return found.Count switch
        {
            1 => found[0].Path,
            0 => throw new InvalidOperationException(
                $"print: {Endpoint.Path} declares no print form named '{asked}'. It has: {names}"),
            _ => throw new InvalidOperationException(
                $"print: {Endpoint.Path} declares {found.Count} print forms named '{asked}'")
        };
    }

    /* Addressed by path from the endpoint's own folder, extension implied - the way a view is
     * named. A blank is not an endpoint and has no folder of its own.
     */
    private async Task<String> ReadPrintFormAsync(String path)
    {
        var codeProvider = serviceProvider.GetRequiredService<IAppCodeProvider>();
        var fileName = $"{Endpoint.Path.Trim('/')}/{path}.json";
        using var stream = codeProvider.FileStreamRO(fileName)
            ?? throw new InvalidOperationException($"print: '{fileName}' not found");
        using var sr = new StreamReader(stream);
        return await sr.ReadToEndAsync();
    }
}

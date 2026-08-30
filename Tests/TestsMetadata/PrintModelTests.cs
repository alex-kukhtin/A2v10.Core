// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;

namespace A2v10.Metadata.Tests;

/* The print fetch, built against a real shape. No database is touched: everything that can be wrong
 * about a blank is decided while the text is generated, which is what makes these assertions cheap.
 *
 * Names are taken from the metadata rather than typed as literals - the point is that the builder
 * and the shape agree, not that a particular string was produced.
 */
public class PrintModelTests
{
    static async Task<NormalEndpointMetadata> WaybillInAsync() =>
        (NormalEndpointMetadata)await TestHost.GetService<DatabaseMetadataProvider>()
            .GetEndpointAsync(null, "document", "waybillin");

    static PrintNode Node(String name, params String[] fields) => new(name, false, fields, []);
    static PrintNode Node(String name, String[] fields, params PrintNode[] nodes)
        => new(name, false, fields, nodes);
    static PrintNode Rows(String name, String[] fields, params PrintNode[] nodes)
        => new(name, true, fields, nodes);
    static PrintNode Root(params PrintNode[] nodes) => new("Document", false, ["Date"], nodes);

    static String Build(TableMetadata doc, PrintNode model) => new PrintSqlBuilder(doc, model).Build();

    // ---- what the record carries -----------------------------------------------------------

    [Fact]
    public async Task Record_carries_scalars_references_and_a_slot_per_collection()
    {
        var doc = (await WaybillInAsync()).Storage;
        var rows = doc.Details["Rows"];
        var agent = doc.Columns.First(c => c.Name == "Agent").RefTableCheck.Storage;

        var sql = Build(doc, Root(Node("Agent", "Name"), Rows("Rows", ["Qty"])));

        Assert.Contains($"select [{doc.Model}!{doc.TypeName}!Object] = null", sql);
        Assert.Contains("[Id!!Id] = t0.[Id]", sql);       // implicit, never written in the blank
        Assert.Contains("t0.[Date]", sql);
        Assert.Contains($"[Agent!{agent.RefTypeName}!RefId] = t0.[Agent]", sql);
        Assert.Contains($"[Rows!{rows.TypeName}!Array] = null", sql);
    }

    // ---- the two ways a collection is addressed ---------------------------------------------

    /* The declared key of 'details' is every row of it; the composed name of a kind is that kind
     * alone. Both are the same table under different types, and a blank may use either.
     */
    [Fact]
    public async Task Collection_by_details_key_takes_every_row()
    {
        var doc = (await WaybillInAsync()).Storage;
        var rows = doc.Details["Rows"];

        var sql = Build(doc, Root(Rows("Rows", ["Qty"])));

        Assert.Contains($"select [!{rows.TypeName}!Array] = null", sql);
        Assert.Contains($"[!{doc.TypeName}.Rows!ParentId] = t1.[Owner]", sql);
        Assert.DoesNotContain(rows.RowKindField, sql);
    }

    [Fact]
    public async Task Collection_by_kind_name_takes_that_kind_only()
    {
        var doc = (await WaybillInAsync()).Storage;
        var rows = doc.Details["Rows"];
        var name = rows.KindCollectionName("Service");

        var sql = Build(doc, Root(Rows(name, ["Sum"])));

        Assert.Contains($"select [!{rows.KindTypeName("Service")}!Array] = null", sql);
        Assert.Contains($"and t1.[{rows.RowKindField}] = N'Service'", sql);
    }

    /* All the lines, and then the services again by themselves - one table, two recordsets, two
     * types, two slots. Neither name is the platform's to refuse here.
     */
    [Fact]
    public async Task Both_names_of_one_collection_may_appear_in_one_blank()
    {
        var doc = (await WaybillInAsync()).Storage;
        var rows = doc.Details["Rows"];
        var service = rows.KindCollectionName("Service");

        var sql = Build(doc, Root(Rows("Rows", ["Qty"]), Rows(service, ["Sum"])));

        Assert.Contains($"[Rows!{rows.TypeName}!Array] = null", sql);
        Assert.Contains($"[{service}!{rows.KindTypeName("Service")}!Array] = null", sql);
        Assert.Equal(2, sql.Split('\n').Count(l => l.Contains($"from {rows.SqlTableName} ")));
    }

    // ---- the maps ---------------------------------------------------------------------------

    [Fact]
    public async Task Reference_inside_a_collection_is_driven_by_the_rows()
    {
        var doc = (await WaybillInAsync()).Storage;
        var rows = doc.Details["Rows"];
        var item = rows.Columns.First(c => c.Name == "Item").RefTableCheck.Storage;

        var sql = Build(doc, Root(Rows("Rows", ["Qty"], Node("Item", "Name"))));

        Assert.Contains($"select [!{item.RefTypeName}!Map] = null", sql);
        Assert.Contains($"select t1.[Item] from {rows.SqlTableName} t1", sql);
    }

    /* Two different paths reach one table - directly, and through the agent. A table is ONE map, so
     * its rows are the union of both: emitting it when it is first reached drops the deeper path
     * silently, which is a blank cell and no error.
     */
    [Fact]
    public async Task Two_paths_onto_one_table_make_one_map_over_both()
    {
        var doc = (await WaybillInAsync()).Storage;
        var store = doc.Columns.First(c => c.Name == "StoreFrom").RefTableCheck.Storage;

        var sql = Build(doc, Root(
            Node("StoreFrom", "Name"),
            Node("Agent", [], Node("Store", "Name"))));

        Assert.Equal(1, sql.Split('\n').Count(l => l.Contains($"from {store.SqlTableName} ")));

        var map = sql[sql.IndexOf($"-- {store.Model} map", StringComparison.Ordinal)..];
        Assert.Contains("union all", map);
        Assert.Contains("[StoreFrom]", map);
        Assert.Contains("[Store]", map);
    }

    /* A predicate is reused verbatim inside the subquery of whatever hangs below it, and an
     * unqualified name there binds to the innermost scope that has such a column - so a column the
     * inner table happens to lack becomes a correlated reference to the outer one, silently. The
     * three spellings below are every position a predicate column can appear in.
     */
    [Fact]
    public async Task Every_column_of_every_predicate_is_qualified()
    {
        var doc = (await WaybillInAsync()).Storage;
        var service = doc.Details["Rows"].KindCollectionName("Service");

        var sql = Build(doc, Root(
            Node("Agent", [], Node("Store", "Name")),
            Rows("Rows", ["Qty"], Node("Item", "Name")),
            Rows(service, ["Sum"])));

        Assert.DoesNotContain("where [", sql);
        Assert.DoesNotContain(" and [", sql);
        Assert.DoesNotContain("(select [", sql);
    }

    // ---- what the blank may get wrong --------------------------------------------------------

    static String Error(TableMetadata doc, PrintNode model) =>
        Assert.Throws<InvalidOperationException>(() => Build(doc, model)).Message;

    [Fact]
    public async Task A_name_the_shape_does_not_have_is_named_with_the_table_it_was_sought_in()
    {
        var doc = (await WaybillInAsync()).Storage;
        Assert.Contains($"'Nope' not found in {doc.SqlTableName}",
            Error(doc, new PrintNode("Document", false, ["Nope"], [])));
    }

    [Fact]
    public async Task A_reference_written_as_a_field_says_so()
    {
        var doc = (await WaybillInAsync()).Storage;
        Assert.Contains("is a reference - write it as an object",
            Error(doc, new PrintNode("Document", false, ["Agent"], [])));
    }

    [Fact]
    public async Task A_field_written_as_an_object_says_so()
    {
        var doc = (await WaybillInAsync()).Storage;
        Assert.Contains("is not a reference - write it as a field",
            Error(doc, Root(Node("Date", "Nope"))));
    }

    [Fact]
    public async Task A_collection_written_as_an_object_says_how_to_write_it()
    {
        var doc = (await WaybillInAsync()).Storage;
        Assert.Contains("is a collection - write it as 'Rows[]'",
            Error(doc, Root(Node("Rows", "Qty"))));
    }

    [Fact]
    public async Task A_collection_under_a_reference_is_refused()
    {
        var doc = (await WaybillInAsync()).Storage;
        Assert.Contains("a collection belongs to the record or to another collection",
            Error(doc, Root(Node("Agent", [], Rows("Addresses", ["Text"])))));
    }

    [Fact]
    public async Task A_root_that_is_not_the_model_says_what_it_should_have_been()
    {
        var doc = (await WaybillInAsync()).Storage;
        Assert.Contains($"but {doc.SqlTableName} is '{doc.Model}'",
            Error(doc, new PrintNode("Invoice", false, [], [])));
    }

    // ---- the example blank -------------------------------------------------------------------

    /* The real blank, end to end. It earns its place by what it happens to ask for: 'Unit' both on
     * the row and through 'Item', so cat.Units is reached by two paths at two depths - one map over
     * the union of their ids, carrying the union of their fields.
     */
    [Fact]
    public async Task WaybillIn_blank_resolves_and_shares_one_map_between_its_two_paths()
    {
        var endpoint = await WaybillInAsync();
        var doc = endpoint.Storage;
        var rows = doc.Details["Rows"];
        var unit = rows.Columns.First(c => c.Name == "Unit").RefTableCheck.Storage;

        var form = endpoint.Declaration.PrintForms[0];
        var fileName = $"{endpoint.Path.Trim('/')}/{form.Path}.json";
        using var stream = TestHost.GetService<IAppCodeProvider>().FileStreamRO(fileName)
            ?? throw new InvalidOperationException($"'{fileName}' not found");
        using var sr = new StreamReader(stream);
        var model = PrintModel.Parse(await sr.ReadToEndAsync(TestContext.Current.CancellationToken));

        var sql = Build(doc, model);

        Assert.Equal(1, sql.Split('
').Count(l => l.Contains($"from {unit.SqlTableName} ")));

        var map = sql[sql.IndexOf($"-- {unit.Model} map", StringComparison.Ordinal)..];
        Assert.Contains("union all", map);          // the row's Unit and the item's Unit
        Assert.Contains("[Denom]", map);            // asked for by the deeper path only
    }
}

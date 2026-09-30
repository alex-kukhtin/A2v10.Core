// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;
using A2v10.Metadata.Cli;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* The basis: the document a document was created from. A reference whose target must be a document,
 * set by birth and shown on the card as a link. TestApp: the stock documents hold the order they were
 * created from, and document/order has a table of its own. See a2v10-md-skill, concepts/basedon.
 */
public class BasedOnTests
{
    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table));

    static async Task<String> ViewOf(String endpoint, String action)
    {
        var mat = new EndpointMaterializer(TestHost.GetService<DatabaseMetadataProvider>());
        return (await mat.MaterializeAsync(endpoint, action, MaterializeWhat.View)).Files[0].Text;
    }

    static async Task<SqlBuilder> BuilderOf(String endpointPath, String query)
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var (schema, table) = DatabaseMetadataProvider.ParsePath(endpointPath);
        var descriptor = new BuilderDescriptor()
        {
            Endpoint = await provider.GetNormalEndpointAsync(null, schema, table),
            PlatformUrl = new PlatformUrl($"_page{endpointPath}/edit/new?{query}"),
            PlatformId = await provider.GetPlatformIdAsync(null)
        };
        return new SqlBuilder(descriptor, TestHost.Services);
    }

    // column -> value of the insert that fills a variable; split at ', ', no value there holds a comma
    static Dictionary<String, String> Fill(String sql, String variable)
    {
        var start = sql.IndexOf($"insert into {variable} (");
        Assert.True(start >= 0, $"no insert into {variable}");
        var open = sql.IndexOf('(', start) + 1;
        var columns = sql[open..sql.IndexOf(')', open)].Split(", ").Select(c => c.Trim('[', ']'));
        var select = sql.IndexOf("select ", open) + "select ".Length;
        var values = sql[select..sql.IndexOf('\n', select)].TrimEnd('\r').Split(", ");
        return columns.Zip(values).ToDictionary(p => p.First, p => p.Second);
    }

    // the foreign key goes where the target lives - a document of its own table included
    [Fact]
    public async Task A_basis_points_at_the_table_of_its_document()
    {
        var documents = (await LoadNormalAsync("document", String.Empty)).Storage;

        var order = documents.AllColumns().Single(c => c.Name == "Order");
        var fk = CliDatabaseCreator.CreateForeignKeys(documents);

        Assert.Equal("/document/order", order.RefTableCheck.Path);
        Assert.Equal("platformid", order.ToSqlDbTypeInfo().SqlName);
        Assert.Contains("constraint FK_StockDocuments_Order_Orders foreign key ([Order]) references doc.[Orders]([Id])", fk);
    }

    [Fact]
    public async Task A_basis_pointing_at_a_catalog_is_refused()
    {
        var documents = await LoadNormalAsync("document", String.Empty);
        var agent = await LoadNormalAsync("catalog", "agent");
        var column = new TableColumn("Order", ColumnType.BasedOn) { Target = "/catalog/agent" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => DatabaseMetadataProvider.CheckTargetKind(documents, column, agent.Storage));
        Assert.Contains("a document (/document/<name>)", ex.Message);
    }

    // read through the reference view, so the card has where it opens and how it draws
    [Fact]
    public async Task A_basis_resolves_with_its_address()
    {
        var endpoint = await LoadNormalAsync("document", String.Empty);
        var sql = new RefMapBuilder(endpoint, isPlain: true, hasDefaults: false).GenerateResolves()!;

        var orders = sql.Split("with T as").Single(s => s.Contains("doc.[Orders$Ref]"));
        Assert.Contains("a.[Done], a.[$Url], a.[$Icon]", orders);
    }

    // TestApp's order offers a waybill (one implicit operation) and a receipt from a supplier (an operation of a list)
    [Fact]
    public async Task The_source_resolves_what_it_offers()
    {
        var order = await LoadNormalAsync("document", "order");

        Assert.Collection(order.Declaration.BasedOn,
            b => Assert.Equal("/document/waybillout", b.TargetEndpoint!.Path),
            b =>
            {
                Assert.Equal("/document/receipt", b.TargetEndpoint!.Path);
                Assert.Equal("supplier", b.Operation);
            });
    }

    // the '?Op=' rule: named exactly when the target lists operations, and one of them
    [Theory]
    [InlineData("waybillout", "supplier", "lists none")]
    [InlineData("receipt", null, "name one in 'operation'")]
    [InlineData("receipt", "export", "is not an operation of /document/receipt")]
    public async Task The_operation_of_the_target_follows_its_list(String target, String? operation, String message)
    {
        var endpoint = await LoadNormalAsync("document", target);
        var b = new BasedOnMetadata() { Target = endpoint.Path, Operation = operation };

        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckBasedOnOperation("basedOn", b, endpoint));
        Assert.Contains(message, ex.Message);
    }

    // the target finds its entry by the source's path and its own, so one entry per target
    [Fact]
    public void A_target_is_offered_once()
    {
        var declaration = new DeclarationMetadata()
        {
            BasedOn = [
                new() { Target = "/document/waybillout" },
                new() { Target = "/document/receipt", Operation = "supplier" },
                new() { Target = "/Document/WaybillOut" }
            ]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOfferedOnce("/document/x", declaration));
        Assert.Contains("/document/waybillout is offered twice", ex.Message);
    }

    // one item per entry: the target's birth with the basis by id and address, the operation by '?Op='
    [Theory]
    [InlineData("edit", "Open", "Order")]
    [InlineData("index", "OpenSelected", "Parent.ItemsSource")]
    public async Task The_source_offers_its_targets_on_both_bars(String action, String command, String argument)
    {
        // an attribute of XML: the writer spells '&' as '&amp;'
        var lines = (await ViewOf("/document/order", action)).Replace("&amp;", "&").Split('\n');

        Assert.Contains(lines, l => l.Contains("""Content="@[BasedOn]"""));
        var waybill = lines.Single(l => l.Contains("@[Operation.waybillout]"));
        Assert.Contains($"BindCmd {command}", waybill);
        Assert.Contains("/document/waybillout/edit/new?BasedOn={0}&Base=/document/order", waybill);
        Assert.Contains($"Argument={{Bind {argument}}}", waybill);
        var receipt = lines.Single(l => l.Contains("@[Operation.receipt.supplier]"));
        Assert.Contains("/document/receipt/edit/new?Op=supplier&BasedOn={0}&Base=/document/order", receipt);
    }

    /* A receipt from a supplier on basis of an order (TestApp: the order's 'basedOn'). The record is
     * filled into a variable of the receipt's shape: the basis, the one deviation the file writes, the
     * rest by name and domain - and what is the record's own or issued to it is not taken. A column with
     * an initial value the source did not give is left null, for the defaults to fill.
     */
    [Fact]
    public async Task Birth_fills_the_record_as_the_file_says()
    {
        var sql = await (await BuilderOf("/document/receipt", "Op=supplier&BasedOn=5&Base=/document/order")).BuildBirthSqlTextAsync();        var record = Fill(sql, "@$Record");

        Assert.Equal("cast(N'0' as bigint)", record["Id"]);
        Assert.Equal("s.[Id]", record["Order"]);             // the basis
        Assert.Equal("s.[Store]", record["StoreTo"]);        // 'document'
        Assert.Equal("s.[Agent]", record["Agent"]);          // by name and domain
        Assert.Equal("s.[Sum]", record["Sum"]);
        Assert.Equal("null", record["StoreFrom"]);           // nothing of that name there
        Assert.Equal("null", record["Number"]);              // issued at save
        Assert.Equal("null", record["Operation"]);           // the url's, by the defaults
        Assert.Equal("null", record["State"]);               // the set's initial state, by the defaults
        Assert.Equal("0", record["Done"]);                   // the record's own
        Assert.Equal("null", record["Date"]);
        Assert.Contains("from doc.[Orders] s where s.[Id] = @BasedOn;", sql);
    }

    // the order's rows have no kinds, so all of them become the one kind named; inherit fills what they lack
    [Fact]
    public async Task Birth_fills_the_rows_into_the_kind_named()
    {
        var order = (await LoadNormalAsync("document", "order")).Storage.Details["Rows"];
        var rows = (await LoadNormalAsync("document", String.Empty)).Storage.Details["Rows"];
        var sql = await (await BuilderOf("/document/receipt", "Op=supplier&BasedOn=5&Base=/document/order")).BuildBirthSqlTextAsync();
        var row = Fill(sql, "@$Record$Rows");

        Assert.Equal("null", row["Id"]);
        Assert.Equal("cast(N'0' as bigint)", row[rows.MasterField]);
        Assert.Equal("N'Stock'", row["Kind"]);
        Assert.Equal("r.[RowNo]", row["RowNo"]);
        Assert.Equal("r.[Item]", row["Item"]);
        Assert.Equal("r.[Qty]", row["Qty"]);
        Assert.Equal("null", row["Unit"]);
        Assert.Contains($"from {order.SqlTableName} r where r.[{order.MasterField}] = @BasedOn;", sql);
        Assert.Contains("update v set v.[Unit] = t.[Unit]", sql);
        Assert.Contains("where v.[VatRate] is null and v.[Kind] = N'Stock';", sql);
    }

    /* A customer return from a shipment - both over one table, as in the skill's example. Every column
     * meets itself: another basis rides along (the shipment's order), and kinds of the source meet the
     * target's by name, the other kinds of the shipment left behind.
     */
    [Fact]
    public async Task Birth_between_documents_of_one_table()
    {
        var rows = (await LoadNormalAsync("document", String.Empty)).Storage.Details["Rows"];
        var sql = await (await BuilderOf("/document/waybillreturn", "BasedOn=5&Base=/document/waybillout")).BuildBirthSqlTextAsync();
        var record = Fill(sql, "@$Record");
        var row = Fill(sql, "@$Record$Rows");

        Assert.Equal("s.[Id]", record["Shipment"]);          // the basis
        Assert.Equal("s.[Order]", record["Order"]);          // the shipment's own basis, by name
        Assert.Equal("s.[StoreFrom]", record["StoreFrom"]);
        Assert.Equal("s.[Agent]", record["Agent"]);
        Assert.Equal("null", record["Number"]);
        Assert.Equal("null", record["Operation"]);           // the return's own, by the defaults
        Assert.Equal("N'Stock'", row["Kind"]);
        Assert.Equal("r.[Item]", row["Item"]);
        Assert.Equal("r.[VatRate]", row["VatRate"]);         // the source has it, inherit fills only what is empty
        Assert.Contains("from doc.[StockDocuments] s where s.[Id] = @BasedOn;", sql);
        Assert.Contains($"from {rows.SqlTableName} r where r.[{rows.MasterField}] = @BasedOn and r.[Kind] = N'Stock';", sql);
        Assert.Equal(2, sql.Split("insert into @$Record$Rows (").Length);   // one insert: Service stays behind
    }

    // past the fill the load is that of any new record, reading the variables: recordsets, the map, the defaults
    [Fact]
    public async Task A_born_record_is_loaded_from_the_variables()
    {
        var rows = (await LoadNormalAsync("document", String.Empty)).Storage.Details["Rows"];
        var sql = await (await BuilderOf("/document/receipt", "Op=supplier&BasedOn=5&Base=/document/order")).BuildBirthSqlTextAsync();

        Assert.Contains("from @$Record a where a.Id = cast(N'0' as bigint);", sql);
        Assert.Contains($"from @$Record$Rows d where d.[{rows.MasterField}] = cast(N'0' as bigint) and d.[Kind] = N'Stock'", sql);
        Assert.Contains("from @$Record$Rows\nwhere", sql.Replace("\r", ""));
        Assert.Contains("[Document.Operation!TROperation!RefId] = @InitOperation", sql);
        Assert.Contains("[Document.State!TROrderState!RefId] = N'new'", sql);
    }

    // a new document with no basis keeps its text: the tables, the id, the defaults
    [Fact]
    public async Task A_new_record_without_a_basis_is_loaded_as_before()
    {
        var sql = (await BuilderOf("/document/waybillout", "")).BuildLoadPlainSqlText();

        Assert.Contains("from doc.[StockDocuments] a where a.Id = @Id;", sql);
        Assert.Contains("[Document.Date!!Utc] = a2meta.fn_getUtcDate()", sql);
        Assert.Contains("!RefId] = N'waybillout'", sql);
        Assert.DoesNotContain("@$Record", sql);
    }

    [Theory]
    [InlineData("/document/receipt", "BasedOn=5", "without '?Base='")]
    [InlineData("/document/waybillin", "BasedOn=5&Base=/document/order", "does not offer /document/waybillin")]
    public async Task A_birth_the_source_does_not_offer_is_refused(String endpoint, String query, String message)
    {
        var builder = await BuilderOf(endpoint, query);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(builder.BuildBirthSqlTextAsync);
        Assert.Contains(message, ex.Message);
    }

    // what the file copies is checked at the source's load, where the file is
    [Theory]
    [InlineData("""{ "target": "/document/receipt", "document": { "Nope": "Store" } }""", "[Nope], which /document does not declare")]
    [InlineData("""{ "target": "/document/receipt", "document": { "Number": "Number" } }""", "issued to the new document")]
    [InlineData("""{ "target": "/document/receipt", "document": { "StoreTo": "Agent" } }""", "targets '/catalog/store'")]
    [InlineData("""{ "target": "/document/receipt", "each": { "details": "Rows", "kinds": [ "Stock", "Service" ] } }""", "name one in 'kinds'")]
    public async Task What_cannot_be_copied_is_refused(String json, String message)
    {
        var order = (await LoadNormalAsync("document", "order")).Storage;
        var documents = (await LoadNormalAsync("document", String.Empty)).Storage;
        var entry = Newtonsoft.Json.JsonConvert.DeserializeObject<BasedOnMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            BasedOnMapping.CheckDocument("basedOn", entry, documents, order);
            BasedOnMapping.Rows("basedOn", entry, documents, order);
        });
        Assert.Contains(message, ex.Message);
    }

    // birth sets it and nothing else does: no picker, a link to the basis
    [Fact]
    public async Task The_basis_on_a_card_is_a_link_not_a_picker()
    {
        var lines = (await ViewOf("/document/waybillin", "edit")).Split('\n');

        var link = lines.Single(l => l.Contains("<Hyperlink") && l.Contains("Document.Order"));
        Assert.Contains("""If="{Bind Document.Order.Id}""", link);
        Assert.Contains("""Content="{Bind Document.Order.Name}""", link);
        Assert.Contains("""Icon="{Bind Document.Order.$Icon}""", link);
        Assert.Contains("Url={Bind Document.Order.$Url}", link);
        Assert.DoesNotContain(lines, l => l.Contains("<SelectorSimple") && l.Contains("Document.Order"));
    }
}

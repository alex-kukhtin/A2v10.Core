// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The operations of a document: what the user switches inside an open one. TestApp lays
 * document/receipt over /document with two of them, each posting in its own file.
 * See a2v10-md-skill, concepts/operations.
 */
public class OperationTests
{
    const String Document = "document";
    const String File = "document/x/metadata.json";

    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table));

    // the code is '<document>.<name>': unique because the document's name is, and nothing to check
    [Fact]
    public async Task A_listed_operation_is_coded_by_its_document_and_its_name()
    {
        var receipt = await LoadNormalAsync(Document, "receipt");

        Assert.Equal(["receipt.supplier", "receipt.gratis"], receipt.DocumentOperations());
        Assert.All(receipt.Declaration.OperationDeclarations, o => Assert.NotEmpty(o.Post));
    }

    // one implicit operation, named as the document: no migration for what is deployed today
    [Fact]
    public async Task A_document_over_a_storage_listing_none_is_one_operation_named_as_itself()
    {
        var waybill = await LoadNormalAsync(Document, "waybillin");

        Assert.Equal(["waybillin"], waybill.DocumentOperations());
        Assert.Empty(waybill.Declaration.OperationDeclarations);
    }

    /* The platform PascalCases every query key, so the key is spelled that way from the start. Pinned
     * because it was missed once: the lookup asked for 'op', found nothing and started on the first.
     */
    [Fact]
    public void The_url_parameter_is_found_under_the_name_it_is_written()
    {
        var url = new A2v10.Services.PlatformUrl($"/_page/document/receipt/edit/new?{Constants.FieldNames.OperationQuery}=gratis");

        var query = Assert.IsAssignableFrom<IDictionary<String, Object?>>(url.Query);
        Assert.Equal("gratis", query[Constants.FieldNames.OperationQuery]);
    }

    [Fact]
    public async Task A_new_document_starts_on_the_first_or_on_the_one_the_url_names()
    {
        var receipt = await LoadNormalAsync(Document, "receipt");

        var op = receipt.AllInitials()["Operation"];
        Assert.Equal(InitialSource.Query, op.Source);
        Assert.Equal(Constants.FieldNames.OperationQuery, op.Value);

        Assert.Equal("receipt.supplier", receipt.StartOperation(null));
        Assert.Equal("receipt.gratis", receipt.StartOperation("gratis"));
        // another document's operation is a wrong link, not an empty field
        Assert.Throws<InvalidOperationException>(() => receipt.StartOperation("waybillin"));
    }

    // exactly one 'post' is in force: the one of the code the document carries when it is posted
    [Fact]
    public async Task Posting_is_chosen_by_the_code_in_the_row()
    {
        var receipt = await LoadNormalAsync(Document, "receipt");

        var statements = new PostStatements(receipt);

        Assert.Contains("if @OperationId = N'receipt.supplier'", statements.Post);
        Assert.Contains("if @OperationId = N'receipt.gratis'", statements.Post);
        Assert.Contains("if @OperationId = N'receipt.gratis'", statements.UnPost);
        Assert.Equal(2, receipt.Declaration.Posts().Count());
    }

    /* The page is titled by the document, and the operation is picked in the taskpad among the
     * document's own candidates - by the element, as a set's value is.
     */
    [Fact]
    public async Task The_page_is_titled_by_the_document_and_the_operation_is_picked_in_the_taskpad()
    {
        var mat = new EndpointMaterializer(TestHost.GetService<DatabaseMetadataProvider>());
        var xaml = (await mat.MaterializeAsync("/document/receipt", "edit", MaterializeWhat.View)).Files[0].Text;

        Assert.Contains("@[Operation.receipt]", xaml);
        var taskpad = xaml[xaml.IndexOf("<Page.Taskpad>")..];
        Assert.Contains("<ComboBox", taskpad);
        Assert.Contains("""ItemsSource="{Bind Operations}""", taskpad);
        Assert.DoesNotContain("{Bind Document.Operation.Name}", xaml);
    }

    // created ON an operation: one item each, in the order of the list, the url carrying its name
    [Fact]
    public async Task Create_offers_the_operations_of_the_document()
    {
        var mat = new EndpointMaterializer(TestHost.GetService<DatabaseMetadataProvider>());
        var xaml = (await mat.MaterializeAsync("/document/receipt", "index", MaterializeWhat.View)).Files[0].Text;

        var supplier = xaml.IndexOf("/document/receipt/edit/{0}?Op=supplier");
        var gratis = xaml.IndexOf("/document/receipt/edit/{0}?Op=gratis");
        Assert.True(supplier >= 0 && gratis > supplier);
        Assert.Contains("@[Operation.receipt.gratis]", xaml);
    }

    // the filter picks from the same list, written from the same file: the code, and 'All' first
    [Fact]
    public async Task The_filter_offers_the_operations_of_the_document()
    {
        var mat = new EndpointMaterializer(TestHost.GetService<DatabaseMetadataProvider>());
        var xaml = (await mat.MaterializeAsync("/document/receipt", "index", MaterializeWhat.View)).Files[0].Text;

        var all = xaml.IndexOf("@[Operation.All]");
        var gratis = xaml.IndexOf("""Value="receipt.gratis" """.TrimEnd());
        Assert.True(all >= 0 && gratis > all);
    }

    [Fact]
    public async Task A_document_that_is_one_operation_keeps_its_name_as_the_title()
    {
        var mat = new EndpointMaterializer(TestHost.GetService<DatabaseMetadataProvider>());
        var xaml = (await mat.MaterializeAsync("/document/waybillmove", "edit", MaterializeWhat.View)).Files[0].Text;

        Assert.Contains("{Bind Document.Operation.Name}", xaml);
        Assert.DoesNotContain("<Page.Taskpad>", xaml);
    }

    /* One column, three answers: every document's at the storage, a choice among the document's own
     * where it lists several, and nothing to choose where the address is the operation.
     */
    [Theory]
    [InlineData("", FilterKind.Ref)]
    [InlineData("receipt", FilterKind.Set)]
    [InlineData("waybillmove", null)]
    public async Task The_operation_filters_as_the_endpoint_says(String document, FilterKind? kind)
    {
        var endpoint = await LoadNormalAsync(Document, document);
        var filter = endpoint.Storage.Filters(endpoint.Declaration).SingleOrDefault(f => f.Name == "Operation");

        Assert.Equal(kind, filter?.Kind);
    }

    [Fact]
    public void The_storage_has_no_operations() =>
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOperations(
            File, String.Empty, new DeclarationMetadata() { Table = "T", Operations = ["a"] }, ["a"]));

    [Theory]
    [InlineData("1a")]
    [InlineData("a.b")]
    [InlineData("a-b")]
    public void A_name_is_an_identifier(String name) =>
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOperations(
            File, "x", new DeclarationMetadata() { Storage = "/document", Operations = [name] }, [name]));

    [Fact]
    public void A_name_is_listed_once() =>
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOperations(
            File, "x", new DeclarationMetadata() { Storage = "/document", Operations = ["a", "a"] }, ["a"]));

    [Fact]
    public void Post_is_written_in_the_operations_and_not_in_the_document() =>
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOperations(
            File, "x", new DeclarationMetadata() { Storage = "/document", Operations = ["a"], Post = [] }, ["a"]));

    [Fact]
    public void A_name_without_a_file_is_refused() =>
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOperations(
            File, "x", new DeclarationMetadata() { Storage = "/document", Operations = ["a", "b"] }, ["a"]));

    // a list naming some of the files, and no list at all
    [Theory]
    [InlineData("a")]
    [InlineData(null)]
    public void A_file_nobody_lists_is_refused(String? listed) =>
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOperations(
            File, "x", new DeclarationMetadata() { Storage = "/document", Operations = listed == null ? [] : [listed] }, ["a", "b"]));

    [Fact]
    public void An_operation_without_post_is_refused() =>
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.ToOperationDeclaration(
            File, "x", "a", new OperationFileMetadata()));

    // not generated yet: dropped, they would be written and never hold
    [Fact]
    public void Rules_of_an_operation_are_refused_until_they_are_generated() =>
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.ToOperationDeclaration(
            File, "x", "a", new OperationFileMetadata() { Post = [new PostMetadata()], Rules = new() { Required = ["Agent"] } }));

    [Fact]
    public async Task Operations_need_a_column_to_hold_the_code()
    {
        var agents = (await LoadNormalAsync("catalog", "agent")).Storage;
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOperationColumn(
            File, new DeclarationMetadata() { Table = "T", Operations = ["a"] }, agents));
    }

    // the rows of the documents over one table are told apart by the code, and by nothing else
    [Fact]
    public async Task A_document_storage_without_the_column_is_refused()
    {
        var agents = (await LoadNormalAsync("catalog", "agent")).Storage;
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOperationColumn(
            File, new DeclarationMetadata() { Storage = "/catalog/agent" }, agents));
    }

    // one value forever: the column carries nothing and reads as a missing 'operations'
    [Fact]
    public async Task A_column_with_one_post_and_no_operations_is_refused()
    {
        var documents = (await LoadNormalAsync(Document, String.Empty)).Storage;
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOperationColumn(
            File, new DeclarationMetadata() { Table = "T", Post = [] }, documents));
    }

    [Fact]
    public async Task The_operation_is_not_an_initial_value()
    {
        var documents = (await LoadNormalAsync(Document, String.Empty)).Storage;
        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckOperationColumn(
            File, new DeclarationMetadata()
            {
                Storage = "/document",
                Operations = ["a"],
                InitialValues = new() { ["Operation"] = new InitialMetadata(InitialSource.Literal, "x.a") }
            }, documents));
    }
}

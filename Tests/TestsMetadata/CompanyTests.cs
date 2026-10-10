// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The company: the enterprise the books are kept for. A reference to the author's catalog that the
 * platform reads - it splits a numbering's counter and gives '{p}'. TestApp numbers document/order
 * by company, its prefix in catalog/company. See a2v10-md-skill, metadata.md -> Company.
 */
public class CompanyTests
{
    const String File = "document/x/metadata.json";

    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table));

    static TableMetadata Table(String path, String json)
    {
        var table = JsonConvert.DeserializeObject<TableMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;
        var segments = path.Trim('/').Split('/');
        table.SetDefaults(segments[0].ToTableKind()!.Value, segments[0], segments[1]);
        return table;
    }

    // loading at all is the chain '{p}' reads: the company column, its catalog, the prefix there
    [Fact]
    public async Task A_numbering_with_the_prefix_loads_over_a_company_whose_catalog_has_one()
    {
        var order = await LoadNormalAsync("document", "order");

        var company = order.Storage.AllColumns().Single(c => c.Type == ColumnType.Company);
        Assert.Equal("/catalog/company", company.RefTableCheck.Path);
    }

    [Fact]
    public async Task The_prefix_without_a_company_column_is_refused()
    {
        var store = await LoadNormalAsync("catalog", "store");

        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckPrefix(
            store, new AutonumMetadata() { Id = "x", Pattern = "{p}-{nnnnn}" }));
    }

    // documents share a numbering, and only some of them have a company: the rest draw from it freely
    [Fact]
    public async Task A_numbering_without_the_prefix_asks_nothing_of_the_company()
    {
        var store = await LoadNormalAsync("catalog", "store");

        DatabaseMetadataProvider.CheckPrefix(store, new AutonumMetadata() { Id = "x", Pattern = "{yyyy}-{nnnnn}" });
    }

    [Fact]
    public void Two_companies_in_one_record_are_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckCompanyColumn(Table("/document/x", """
            { "table": "T", "fields": { "Number": { "type": "autonum" },
                "Company": { "type": "company", "target": "/catalog/company" },
                "CompanyTo": { "type": "company", "target": "/catalog/company" } } }
            """), "document", "x"));
        Assert.Contains("[CompanyTo]", ex.Message);
    }

    // a row belongs to its record, and the record to one company
    [Fact]
    public void A_company_in_a_collection_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckCompanyColumn(Table("/document/x", """
            { "table": "T", "fields": { "Number": { "type": "autonum" } }, "details": { "Rows": { "fields": {
                "Company": { "type": "company", "target": "/catalog/company" } } } } }
            """), "document", "x"));
        Assert.Contains("'Rows'", ex.Message);
    }

    // the counters of one numbering hold the companies of all its documents; two catalogs would collide there
    [Fact]
    public void Companies_are_kept_in_one_catalog()
    {
        var a = Table("/document/a", """{ "table": "A", "fields": { "Number": { "type": "autonum" }, "Company": { "type": "company", "target": "/catalog/company" } } }""");
        var b = Table("/document/b", """{ "table": "B", "fields": { "Number": { "type": "autonum" }, "Firm": { "type": "company", "target": "/catalog/firm" } } }""");
        var c = Table("/document/c", """{ "table": "C", "fields": { "Number": { "type": "autonum" }, "Company": { "type": "company", "target": "/catalog/company" } } }""");

        SqlDbGenerator.CheckOneCompanyCatalog([a, c]);
        var ex = Assert.Throws<InvalidOperationException>(() => SqlDbGenerator.CheckOneCompanyCatalog([a, b, c]));
        Assert.Contains("/catalog/firm", ex.Message);
    }

    // every company counts on its own: the company is in the key, and null meets null
    [Fact]
    public async Task The_counter_is_keyed_by_the_company()
    {
        var registry = (await LoadNormalAsync("autonum", String.Empty)).Storage;

        var values = TableMetadataDefaults.CreateAutonumValuesTable(registry);
        Assert.Contains(Constants.FieldNames.Company, values.Indexes.Single().Columns);

        var sql = SqlDbGenerator.CreateAutonumProcedureScript([registry]);
        Assert.Contains("@Company platformid = null", sql);
        Assert.Contains("t.[Company] is null and s.[Company] is null", sql);
    }

    /* The company of a movement is its document's, found by type: the journal names it [Firm] and the
     * document [Company], and no block says so.
     */
    [Fact]
    public async Task A_journal_takes_the_company_of_the_document()
    {
        var waybill = await LoadNormalAsync("document", "waybillin");

        var post = new PostStatements(waybill).Post;

        Assert.Contains("[Firm]", post);
        Assert.Contains("d.[Company]", post);
    }

    // one company for both legs: a debit in one and a credit in another balance neither
    [Fact]
    public async Task Both_legs_of_a_ledger_carry_the_company_of_the_document()
    {
        var waybill = await LoadNormalAsync("document", "waybillout");

        var post = new PostStatements(waybill).Post;

        Assert.Contains("[Company] = d.[Company]", post);
    }

    static TableColumn CompanyOf(TableMetadata journal) =>
        journal.AllColumns().Single(c => c.Type == ColumnType.Company);

    // two spellings of one value could disagree
    [Fact]
    public async Task A_post_block_naming_the_company_is_refused()
    {
        var documents = (await LoadNormalAsync("document", String.Empty)).Storage;
        var journal = (await LoadNormalAsync("journal", "stock")).Storage;
        var ledger = (await LoadNormalAsync("ledger", "national")).Storage;

        Assert.Throws<InvalidOperationException>(() => PostStatements.CompanyValue(documents, journal, CompanyOf(journal), "p",
            new PostMetadata() { Journal = "/journal/stock", Document = new() { ["Firm"] = "Company" } }));
        Assert.Throws<InvalidOperationException>(() => PostStatements.CompanyValue(documents, ledger, CompanyOf(ledger), "p",
            new PostMetadata() { Ledger = "/ledger/national", Dt = new() { Document = new() { ["Company"] = "Company" } }, Ct = new() }));
    }

    // the other way round is legal: a journal that does not split by company
    [Fact]
    public async Task A_journal_with_a_company_over_a_document_without_one_is_refused()
    {
        var stores = (await LoadNormalAsync("catalog", "store")).Storage;
        var journal = (await LoadNormalAsync("journal", "stock")).Storage;

        var ex = Assert.Throws<InvalidOperationException>(() => PostStatements.CompanyValue(stores, journal, CompanyOf(journal), "p",
            new PostMetadata() { Journal = "/journal/stock" }));
        Assert.Contains("declares no column of type 'company'", ex.Message);
    }

    /* A live database keeps the index of the old key - still unique, refusing the first company's
     * counter row. The deploy drops what the table no longer declares, and only on a table whose
     * indexes the platform declares: an author's table is not touched at all.
     */
    [Fact]
    public async Task The_counters_lose_an_index_of_their_old_key()
    {
        var registry = (await LoadNormalAsync("autonum", String.Empty)).Storage;
        var values = TableMetadataDefaults.CreateAutonumValuesTable(registry);

        var sql = A2v10.Metadata.Cli.CliDatabaseCreator.CreateIndexes(values);

        Assert.Contains("drop index", sql);
        Assert.Contains("name not in (N'UX_Autonum$Values_Autonum_Company_Year_Quart_Month')", sql);
        Assert.True(sql.IndexOf("drop index") < sql.IndexOf("create unique index"));

        var agents = (await LoadNormalAsync("catalog", "agent")).Storage;
        Assert.Empty(A2v10.Metadata.Cli.CliDatabaseCreator.CreateIndexes(agents));
    }

    // after the counter: a prefix is the user's text, and one holding '{n' must not be taken for it
    [Fact]
    public async Task The_prefix_is_substituted_last_and_never_as_null()
    {
        var registry = (await LoadNormalAsync("autonum", String.Empty)).Storage;

        var sql = SqlDbGenerator.CreateAutonumProcedureScript([registry]);
        var prefix = sql.IndexOf("replace(@Number, N'{p}', isnull(@Prefix, N''))");
        Assert.True(prefix > sql.IndexOf("charindex(N'{n', @Number)"));
    }
}

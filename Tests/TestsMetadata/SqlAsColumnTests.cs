// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;
using A2v10.Metadata.Cli;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* 'sqlAs', a column the database computes: 'as cast(<expr> as <type>)', read like any column, written by nothing. TestApp:
 * catalog/unit [Title], the rows of /document [Cost], journal/stock [SignedQty]; the chart's
 * baseline [DisplayName]. See a2v10-md-skill, metadata.md -> the invariants of a field.
 */
public class SqlAsColumnTests
{
    static readonly AppPlatformId PlatformId = new(typeof(Int64));

    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table));

    static async Task<TableMetadata> UnitAsync() => (await LoadNormalAsync("catalog", "unit")).Storage;

    static TableColumn Column(TableMetadata table, String name) => table.AllColumns().Single(c => c.Name == name);

    [Fact]
    public async Task It_is_created_as_a_cast_of_its_expression()
    {
        var unit = await UnitAsync();

        var ddl = new CliDatabaseCreator().CreateTable(unit, PlatformId);

        Assert.Contains("[Title] as cast((concat([Short], N' ', [Name])) as nvarchar(40))", ddl);
    }

    // the expression is the value, NULL included: no default even where the domain has a zero
    [Fact]
    public async Task It_has_no_default_whatever_its_domain()
    {
        var cost = Column((await LoadNormalAsync("document", String.Empty)).Storage.Details["Rows"], "Cost");

        Assert.True(cost.HasZero);
        Assert.Null(cost.DeployDefault());
        Assert.True(cost.DeployNullable());
    }

    // the table type, and the DataTable built from the same answer
    [Fact]
    public async Task Nothing_sends_it()
    {
        var unit = await UnitAsync();
        var rows = (await LoadNormalAsync("document", String.Empty)).Storage.Details["Rows"];

        Assert.DoesNotContain("[Title]", CliDatabaseCreator.CreateTableType(unit));
        Assert.DoesNotContain("[Cost]", CliDatabaseCreator.CreateTableType(rows));
        Assert.False(Column(unit, "Title").IsFieldUpdated());
        Assert.False(Column(unit, "Title").IsFieldInserted());
    }

    // the seed carries the expression, so a new one moves the hash and SyncSchema can add the column
    [Fact]
    public async Task The_seed_carries_the_expression()
    {
        var seed = await SqlDbGenerator.GenerateMetadataSeedAsync([await UnitAsync()]);

        Assert.Contains("N'Title', N'nvarchar', 40, null, null, 1, null, null, null, N'concat([Short], N'' '', [Name])')", seed);
    }

    // auto-mapping walks the journal's columns; one nobody writes is not among the targets
    [Fact]
    public async Task A_posting_does_not_map_it()
    {
        var waybill = await LoadNormalAsync("document", "waybillout");

        var post = new PostStatements(waybill).Post;

        Assert.DoesNotContain("SignedQty", post);
    }

    // computed in the variable as in the table, so the new card shows it; never filled
    [Fact]
    public async Task A_birth_computes_it_and_does_not_fill_it()
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var rows = (await LoadNormalAsync("document", String.Empty)).Storage.Details["Rows"];
        var builder = new SqlBuilder(new BuilderDescriptor()
        {
            Endpoint = await provider.GetNormalEndpointAsync(null, "document", "receipt"),
            PlatformUrl = new PlatformUrl("_page/document/receipt/edit/new?Op=supplier&BasedOn=5&Base=/document/order"),
            PlatformId = await provider.GetPlatformIdAsync(null),
            UseGrants = false,
        }, TestHost.Services);

        var sql = await builder.BuildBirthSqlTextAsync();

        Assert.Contains($"[Cost] {Column(rows, "Cost").SqlAsDefinition()}", sql);
        var insert = sql.IndexOf("insert into @$Record$Rows (");
        Assert.DoesNotContain("[Cost]", sql[insert..sql.IndexOf(')', insert)]);
    }

    [Theory]
    [InlineData("""{ "initialValues": { "Title": { "source": "literal", "value": "x" } } }""", "initialValues: 'Title' has 'sqlAs'")]
    [InlineData("""{ "fixed": { "Title": "x" } }""", "fixed: 'Title' has 'sqlAs'")]
    [InlineData("""{ "rules": { "inherit": { "Title": { "ref": "Short", "field": "Name" } } } }""", "inherit: field 'Title' has 'sqlAs'")]
    public async Task A_declaration_writing_it_is_refused(String json, String message)
    {
        var unit = await UnitAsync();
        var declaration = JsonConvert.DeserializeObject<DeclarationMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;

        var ex = Assert.Throws<InvalidOperationException>(() => declaration.Bake(unit, PlatformId));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public async Task A_birth_copying_into_it_is_refused()
    {
        var unit = await UnitAsync();
        var entry = JsonConvert.DeserializeObject<BasedOnMetadata>(
            """{ "target": "/catalog/unit", "document": { "Title": "Short" } }""", JsonSettings.CamelCaseSerializerSettings)!;

        var ex = Assert.Throws<InvalidOperationException>(() => BasedOnMapping.CheckDocument("basedOn", entry, unit, unit));
        Assert.Contains("[Title], which has 'sqlAs'", ex.Message);
    }

    // the chart computes its code and name in one string, for the places an account is chosen
    [Fact]
    public async Task The_chart_computes_code_and_name()
    {
        var chart = (await LoadNormalAsync("accplan", "national")).Storage;

        var ddl = new CliDatabaseCreator().CreateTable(chart, PlatformId);

        Assert.Contains("[DisplayName] as cast((concat([Id], N' ', [Name])) as nvarchar(320))", ddl);
    }

    // the chart's own screens show the code and the name apart; the card has nothing to type into it
    [Fact]
    public async Task The_charts_own_screens_do_not_show_it()
    {
        var chart = await LoadNormalAsync("accplan", "national");

        foreach (var form in new[] { "index", "browse", "edit" })
            Assert.DoesNotContain(Constants.FieldNames.DisplayName,
                chart.Declaration.BakedForms[form].Body.SelectMany(e => e.Fields));
    }

    [Fact]
    public async Task A_seed_row_naming_it_is_refused()
    {
        var chart = (await LoadNormalAsync("accplan", "national")).Storage;
        var row = JsonConvert.DeserializeObject<Dictionary<String, Newtonsoft.Json.Linq.JToken>>(
            """{ "Name": "x", "AccountType": "Asset", "NormalBalance": "Debit", "DisplayName": "28 x" }""")!;

        Assert.Throws<InvalidOperationException>(() =>
            DatabaseMetadataProvider.AccountRow("accplan/national/seed.json", chart, "28", row));
    }
}

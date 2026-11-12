// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Text.RegularExpressions;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The insert a mapped 'post' becomes. TestApp posts /document/waybillin into /journal/stock; the
 * journal carries an author's date column (DueDate) next to the baseline Date.
 */
public class PostStatementsTests
{
    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table));

    /* The baseline Date is taken by name, as the ledger takes it. Taken by type, it caught every
     * date column of the journal and wrote the document's date into each - the author's DueDate
     * silently got d.[Date], and a 'document' block naming it was never read.
     */
    [Fact]
    public async Task An_authors_date_column_is_mapped_like_any_other_and_not_as_the_baseline_date()
    {
        var waybill = await LoadNormalAsync("document", "waybillin");

        var post = new PostStatements(waybill).Post;

        Assert.Contains("d.[DueDate]", post);
        Assert.Single(Regex.Matches(post, @"d\.\[Date\]"));
    }

    // waybillreturn posts its rows 'out' with storno: Qty and Sum (Qty, Amount) go in negated
    [Fact]
    public async Task Storno_negates_quantity_and_amount()
    {
        var ret = await LoadNormalAsync("document", "waybillreturn");

        var post = new PostStatements(ret).Post;

        Assert.Contains("-r.[Qty]", post);
        Assert.Contains("-r.[Sum]", post);
    }

    /* The return of waybillout's posting in red: Dt 361 Kt 281 again, negated. Signed in the CTE, so
     * the collapsed debit sums the negated rows and the credit leg, one per row, carries -Qty; the
     * debit's Qty is its empty, not a negated one. The sides are the selects' literals, untouched.
     */
    [Fact]
    public async Task Storno_into_a_ledger_negates_the_sum_and_the_legs_measures_and_keeps_the_sides()
    {
        var ret = await LoadNormalAsync("document", "waybillreturn");

        var post = new PostStatements(ret).Post;

        Assert.Contains("[Sum] = -r.[Sum]", post);
        Assert.Contains("[Dt$Qty] = 0, [Ct$Qty] = -r.[Qty]", post);
        Assert.Contains("[Dt$Acc] = N'361', [Ct$Acc] = N'281'", post);
        Assert.Matches(@"select [^\n]*, 1, \[Dt\$Acc\], \[Ct\$Acc\], sum\(\[Sum\]\)", post);
        Assert.Matches(@"select [^\n]*, -1, \[Ct\$Acc\], \[Dt\$Acc\], sum\(\[Sum\]\)", post);
    }

    // of the posting: a leg has no 'storno' to write
    [Fact]
    public void Storno_is_a_key_of_the_ledger_posting() =>
        Newtonsoft.Json.JsonConvert.DeserializeObject<DeclarationMetadata>(
            """{ "post": [ { "ledger": "/ledger/national", "sum": "Sum", "storno": true, "dt": { "const": { "Acc": "281" } }, "ct": { "const": { "Acc": "631" } } } ] }""",
            JsonSettings.CamelCaseSerializerSettings)!.CheckPost("/document/x");

    static void CheckPost(String legs) =>
        Newtonsoft.Json.JsonConvert.DeserializeObject<DeclarationMetadata>(
            $$"""{ "post": [ { "ledger": "/ledger/national", "sum": "Sum", {{legs}} } ] }""",
            JsonSettings.CamelCaseSerializerSettings)!.CheckPost("/document/x");

    [Fact]
    public void An_empty_leg_says_the_other_side_does_not_exist()
    {
        CheckPost("""  "dt": { "const": { "Acc": "281" } }, "ct": {}  """);
        CheckPost("""  "dt": {}, "ct": { "const": { "Acc": "631" } }  """);
    }

    // a forgotten key is not an empty leg, and a posting has a side
    [Theory]
    [InlineData("""  "dt": { "const": { "Acc": "281" } }  """, "declares 'dt', 'ct'")]
    [InlineData("""  "dt": {}, "ct": {}  """, "both legs are empty")]
    public void A_posting_without_a_leg_is_refused(String legs, String message)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CheckPost(legs));
        Assert.Contains(message, ex.Message);
    }

    // CLAUDE.md, "Storno": only what adds up; Float and Decimal are declared Amount/Qty to flip
    [Theory]
    [InlineData(ColumnType.Amount, true)]
    [InlineData(ColumnType.Qty, true)]
    [InlineData(ColumnType.Money, true)]
    [InlineData(ColumnType.Price, false)]
    [InlineData(ColumnType.Percent, false)]
    [InlineData(ColumnType.Factor, false)]
    [InlineData(ColumnType.Float, false)]
    [InlineData(ColumnType.Decimal, false)]
    public void Only_what_adds_up_is_additive(ColumnType type, Boolean additive) =>
        Assert.Equal(additive, new TableColumn("X", type).IsAdditive);
}

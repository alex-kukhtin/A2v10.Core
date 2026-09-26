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

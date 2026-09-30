// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* NormalBalance 'Split': a debit and a credit balance at once, one per object of SplitBy. TestApp:
 * 361 is split by [Agent], waybillout posts its debit there. See a2v10-md-skill, accplan.md.
 */
public class SplitBalanceTests
{
    const String Seed = "accplan/national/seed.json";

    static async Task<EndpointMetadata> LoadAsync(String schema, String table) =>
        await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table);

    static async Task<TableMetadata> ChartAsync() =>
        Assert.IsType<NormalEndpointMetadata>(await LoadAsync("accplan", "national")).Storage;

    static Dictionary<String, JToken> Row(String json) =>
        JsonConvert.DeserializeObject<Dictionary<String, JToken>>(json)!;

    // the file writes an array, the column keeps it joined
    [Fact]
    public async Task A_split_account_keeps_its_columns_joined()
    {
        var chart = await ChartAsync();

        var row = DatabaseMetadataProvider.AccountRow(Seed, chart, "631", Row("""
            { "Name": "x", "AccountType": "Liability", "NormalBalance": "Split", "SplitBy": ["Agent", "Contract"] }
            """));

        Assert.Equal("Agent,Contract", row.Values[Constants.FieldNames.SplitBy]);
        Assert.Equal(["Agent", "Contract"], row.SplitBy);
    }

    [Theory]
    // Split with nothing to split by
    [InlineData("""{ "Name": "x", "AccountType": "Asset", "NormalBalance": "Split" }""")]
    // a list nobody reads
    [InlineData("""{ "Name": "x", "AccountType": "Asset", "NormalBalance": "Both", "SplitBy": ["Agent"] }""")]
    // a string would be a second spelling
    [InlineData("""{ "Name": "x", "AccountType": "Asset", "NormalBalance": "Split", "SplitBy": "Agent" }""")]
    [InlineData("""{ "Name": "x", "AccountType": "Asset", "NormalBalance": "Split", "SplitBy": [] }""")]
    [InlineData("""{ "Name": "x", "AccountType": "Asset", "NormalBalance": "Split", "SplitBy": ["Agent", "Agent"] }""")]
    public async Task A_split_row_that_does_not_hold_together_is_refused(String json)
    {
        var chart = await ChartAsync();

        Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.AccountRow(Seed, chart, "361", Row(json)));
    }

    // the deploy writes the joined list, and null into an account that is not split
    [Fact]
    public async Task The_seed_writes_split_by()
    {
        var chart = await ChartAsync();

        var sql = SqlDbGenerator.CreateSeedScript([chart]);

        var row361 = sql.Split('\n').Single(l => l.TrimStart().StartsWith("(N'361',"));
        var row28 = sql.Split('\n').Single(l => l.TrimStart().StartsWith("(N'28',"));
        Assert.Contains("N'Split', N'Agent'", row361);
        Assert.Contains("N'Debit', null", row28);
    }

    // TestApp's ledger has [Agent]; loading it at all is the check passing
    [Fact]
    public async Task Split_names_a_column_of_every_ledger_over_the_chart()
    {
        var ledger = Assert.IsType<NormalEndpointMetadata>(await LoadAsync("ledger", "national")).Storage;

        DatabaseMetadataProvider.CheckSplitColumns(ledger);
    }

    [Fact]
    public async Task A_leg_onto_a_split_account_without_its_analytics_is_refused()
    {
        var account = (await ChartAsync()).SeedRows.Single(r => r.Id == "361");

        DatabaseMetadataProvider.CheckSplitLeg("p", "dt",
            new() { Const = new() { ["Acc"] = "361" }, Document = new() { ["Agent"] = "Agent" } }, account);
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseMetadataProvider.CheckSplitLeg("p", "dt",
            new() { Const = new() { ["Acc"] = "361" }, Row = new() { ["Item"] = "Item" } }, account));
        Assert.Contains("[Agent]", ex.Message);
    }

    /* The grain: 361 by Agent, every other account by itself. The layout by sign is untouched - Split
     * falls where Both does, one grain finer.
     */
    [Fact]
    public async Task The_trial_balance_lays_a_split_account_out_by_its_analytics()
    {
        var report = Assert.IsType<ReportEndpointMetadata>(await LoadAsync("report", "trialbalance"));
        var builder = Assert.IsType<TrialBalanceReportBuilder>(
            BaseReportBuilder.Create(TestHost.Services, report, new AppPlatformId(typeof(Int64))));

        var (apply, groupBy) = builder.SplitGrain();

        Assert.Contains("case when j.[Acc] in (N'361') then j.[Agent] end", apply);
        Assert.Contains("k([Agent])", apply);
        Assert.Equal(", k.[Agent]", groupBy);
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* A reference is SHOWN by 'presentation' and CHOSEN by 'displayAs'; the element carries both - Name
 * the first, the choice column beside it under its own name. TestApp: the chart is shown by its code
 * and chosen by DisplayName; catalog/unit is shown and chosen by Short. See a2v10-md-skill,
 * metadata.md -> presentation / displayAs.
 */
public class DisplayAsTests
{
    static async Task<TableMetadata> StorageAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table)).Storage;

    static TableMetadata Declared(String schema, String json)
    {
        var table = JsonConvert.DeserializeObject<TableMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;
        table.SetDefaults(schema, "x");
        return table;
    }

    [Fact]
    public async Task An_account_is_shown_by_its_code_and_chosen_by_code_and_name()
    {
        var chart = await StorageAsync("accplan", "national");

        Assert.Equal(Constants.FieldNames.Id, chart.Presentation);
        Assert.Equal(Constants.FieldNames.DisplayName, chart.DisplayAs);
        Assert.Equal(Constants.FieldNames.DisplayName, chart.ChoiceProperty);
    }

    // not written: chosen by what it is shown by, and then the choice is Name itself
    [Fact]
    public async Task Unwritten_it_is_the_presentation()
    {
        var unit = await StorageAsync("catalog", "unit");

        Assert.Equal("Short", unit.DisplayAs);
        Assert.Equal(Constants.FieldNames.Name, unit.ChoiceProperty);
        Assert.Equal(String.Empty, SqlBuilder.ChoiceField(unit, "a"));
    }

    // the map and the candidates hand out one element: the choice column rides beside Name
    [Fact]
    public async Task A_referenced_row_carries_the_choice_column()
    {
        var chart = await StorageAsync("accplan", "national");

        Assert.StartsWith(", a.[DisplayName]", SqlBuilder.RefFields(chart, "a"));
    }

    [Theory]
    [InlineData("""{ "table": "X", "displayAs": "Nope" }""", "displayAs 'Nope' is not a column")]
    [InlineData("""{ "table": "X", "presentation": "Code", "displayAs": "Name", "fields": { "Code": {} } }""", "displayAs is [Name] and presentation [Code]")]
    public void A_choice_column_that_cannot_be_carried_is_refused(String json, String message)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Declared("catalog", json));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void Nothing_chooses_a_journal_row()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Declared("journal", """{ "table": "X", "displayAs": "Id" }"""));
        Assert.Contains("'displayAs' on a Journal", ex.Message);
    }
}

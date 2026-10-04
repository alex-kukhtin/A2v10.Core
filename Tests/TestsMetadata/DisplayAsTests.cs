// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;
using A2v10.Services;

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
        Assert.Null(unit.ChoiceProperty);
        Assert.Equal(String.Empty, SqlBuilder.ChoiceField(unit, "a"));
    }

    // the map and the candidates hand out one element: the choice column rides beside Name
    [Fact]
    public async Task A_referenced_row_carries_the_choice_column()
    {
        var chart = await StorageAsync("accplan", "national");

        Assert.StartsWith(", a.[DisplayName]", SqlBuilder.RefFields(chart, "a"));
    }

    static async Task<String> ViewOf(String endpoint, String action)
    {
        var mat = new EndpointMaterializer(TestHost.GetService<DatabaseMetadataProvider>());
        return (await mat.MaterializeAsync(endpoint, action, MaterializeWhat.View)).Files[0].Text;
    }

    /* Chosen by another column, the selector says which - in the text too, so an ejected view keeps it;
     * chosen by Name, nothing is written: that is the selector's own default.
     */
    [Fact]
    public async Task A_selector_names_the_choice_column_only_when_it_is_not_Name()
    {
        var card = await ViewOf("/catalog/agent", "edit");

        Assert.Contains("""DisplayProperty="DisplayName" """, card);
        Assert.Single(card.Split("DisplayProperty=").Skip(1));
    }

    /* A selector paints itself in the chosen row's colour when the target has one - the row's
     * property, as everywhere else it is drawn; no target colour, nothing written. TestApp: the
     * agent's Region is a coloured catalog, its Account a chart without a colour.
     */
    [Fact]
    public async Task A_selector_is_painted_by_the_target_row()
    {
        var card = await ViewOf("/catalog/agent", "edit");

        Assert.Single(card.Split("ColorProperty=").Skip(1));
        Assert.Contains("""ColorProperty="Color" """, card);
    }

    // the element picked by typing carries the colour as the map's does
    [Fact]
    public async Task A_fetched_element_carries_the_colour()
    {
        var region = await StorageAsync("catalog", "region");

        Assert.Equal(", a.[Color]", SqlBuilder.ColorField(region, "a"));
    }

    // the value is the element itself, the combo's default without an item template - so none is written
    [Fact]
    public async Task A_set_chosen_by_its_name_needs_no_item_template()
    {
        var card = await ViewOf("/catalog/item", "edit");

        Assert.Contains("<ComboBox ", card);
        Assert.DoesNotContain("ComboBoxItem", card);
        Assert.DoesNotContain("DisplayProperty", card);
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

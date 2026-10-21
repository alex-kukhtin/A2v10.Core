// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* A cell of the author's 'fields' in a row a file declares with its table - one reading for every
 * registry (RowCells). The grammars differ in the platform's keys only; an author column any row
 * may name, and a key that is no column fails the load instead of being dropped by the parser.
 *
 * No database: the table is built from text the way the load builds it. See TESTS.md, layer D.
 */
public class RegistryCellsTests
{
    static async Task<TableMetadata> TableAsync(TableKind kind, String schema, String table, String json)
    {
        var storage = JsonConvert.DeserializeObject<TableMetadata>(json, JsonSettings.CamelCaseSerializerSettings)!;
        storage.SetDefaults(kind, schema, table);
        // the code provider is read for a chart's seed file alone
        await RegistryRows.LoadAsync(storage, schema, table, null!);
        return storage;
    }

    static Task<TableMetadata> RatesAsync(String values) => TableAsync(TableKind.Enum, "enum", "vatrate", $$"""
        { "table": "VatRates", "model": "VatRate", "fields": { "Rate": { "type": "percent" } }, "values": [{{values}}] }
        """);

    [Fact]
    public async Task An_enum_value_fills_the_columns_its_author_declared()
    {
        var rates = await RatesAsync("""{ "id": "20", "Rate": 20 }, { "id": "0" }""");

        // a percent is stored as written: 20 is 20%
        Assert.Equal("20", rates.Rows.Single(r => r.Id == "20").Values["Rate"]);
        // not named is not null: the merge leaves the column as it is
        Assert.False(rates.Rows.Single(r => r.Id == "0").Values.ContainsKey("Rate"));
        Assert.False(rates.Rows.Single(r => r.Id == String.Empty).Values.ContainsKey("Rate"));

        var sql = SqlDbGenerator.CreateRowsScript([rates]);
        Assert.Contains($"[Rate] {rates.Columns.Single().SqlDataType()}, [$Rate] bit", sql);
        Assert.Contains("20, 1)", sql);
    }

    [Theory]
    // a typo of the column: the parser used to drop it without a word
    [InlineData("""{ "id": "20", "Rat": 20 }""")]
    // the baseline's own column, written by the platform
    [InlineData("""{ "id": "20", "Order": 3 }""")]
    [InlineData("""{ "id": "20", "Rate": [20] }""")]
    [InlineData("""{ "id": "20", "Rate": "twenty" }""")]
    // an enum has no colour of its own: the author did not declare one
    [InlineData("""{ "id": "20", "color": "green" }""")]
    public async Task A_cell_that_is_no_column_of_the_author_is_refused(String value)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => RatesAsync(value));
    }

    /* The colour is asked of the shape: an enum whose author declared one draws, its 'All' row
     * white like a state's, and the value is checked against the palette as a state's is.
     */
    [Fact]
    public async Task An_enum_with_a_colour_of_its_author_draws_like_a_state()
    {
        const String json = """
            { "table": "Kinds", "model": "Kind", "fields": { "Color": { "type": "color" } }, "values": [{{value}}] }
            """;

        var kinds = await TableAsync(TableKind.Enum, "enum", "kind", json.Replace("{{value}}", """{ "id": "a", "color": "green" }"""));
        Assert.Equal("green", kinds.Rows.Single(r => r.Id == "a").Values["Color"]);
        Assert.Equal("white", kinds.Rows.Single(r => r.Id == String.Empty).Values["Color"]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TableAsync(TableKind.Enum, "enum", "kind", json.Replace("{{value}}", """{ "id": "a", "color": "Green" }""")));
    }

    // the same reading for a numbering: a key beside 'pattern' is a column of the author or nothing
    [Fact]
    public async Task A_numbering_fills_the_columns_its_author_declared()
    {
        static Task<TableMetadata> NumberingsAsync(String cell) => TableAsync(TableKind.Autonum, "autonum", String.Empty, $$"""
            { "table": "Autonums", "fields": { "Note": { "type": "string" } }, "autonums": { "x": { "pattern": "{nnnnn}"{{cell}} } } }
            """);

        var numberings = await NumberingsAsync(""", "Note": "n" """);
        Assert.Equal("n", numberings.Rows.Single().Values["Note"]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => NumberingsAsync(""", "Nte": "n" """));
    }
}

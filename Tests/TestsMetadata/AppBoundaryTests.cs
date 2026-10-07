// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* app.json 'boundary' - the catalogs the user's boundary is cut by. Declared and checked at load; nothing
 * enforces it yet. See AppJson.
 */
public class AppBoundaryTests
{
    static AppJson Parse(String json) =>
        AppJson.From(JsonConvert.DeserializeObject<AppJsonMetadata>(json));

    [Fact]
    public void The_catalogs_are_kept_as_a_path_is_spelled()
    {
        var app = Parse("""{ "boundary": ["/catalog/store", "catalog/Company"] }""");

        Assert.Equal(["/catalog/store", "/catalog/company"], app.Boundary);
    }

    [Fact]
    public void No_boundary_cuts_nothing() =>
        Assert.Empty(Parse("{}").Boundary);

    // the kind is the folder's, through the aliases, as the loader asks it
    [Fact]
    public void A_catalog_under_an_alias_is_a_catalog()
    {
        var app = Parse("""{ "aliases": { "catalog": ["org"] }, "boundary": ["/org/store"] }""");

        Assert.Equal(["/org/store"], app.Boundary);
    }

    [Theory]
    [InlineData("""{ "boundary": ["/document/waybillin"] }""", "'/document/waybillin', which is not a catalog")]
    [InlineData("""{ "boundary": ["/catalog"] }""", "'/catalog', which is not a catalog")]
    [InlineData("""{ "boundary": ["/catalog/store", "/catalog/Store"] }""", "'/catalog/Store' twice")]
    public void A_wrong_dimension_fails_the_load(String json, String message)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Parse(json));
        Assert.Contains(message, ex.Message);
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* A filter rides in the index query under its own name, beside the keys the index reads for itself
 * (Constants.QueryNames). A ref column 'Order' read the sort key and the list came back empty on
 * reload - so such a name is refused at the bake.
 */
public class FilterNameTests
{
    static readonly AppPlatformId PlatformId = new(typeof(Int64));

    [Theory]
    [InlineData("Order")]
    [InlineData("dir")]
    public void A_filter_named_as_a_key_of_the_query_is_refused(String name)
    {
        var table = JsonConvert.DeserializeObject<TableMetadata>(
            $$"""{ "fields": { "{{name}}": { "type": "Ref", "target": "/catalog/agent" } } }""",
            JsonSettings.CamelCaseSerializerSettings)!;
        table.SetDefaults(TableKind.Catalog, "catalog", "x");

        var ex = Assert.Throws<InvalidOperationException>(() => new DeclarationMetadata().Bake(table, PlatformId));
        Assert.Contains($"its filter '{name}' is a key the index query reserves", ex.Message);
    }
}

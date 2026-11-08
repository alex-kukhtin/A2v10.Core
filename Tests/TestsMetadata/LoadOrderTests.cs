// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The order of the fields means nothing. A reference may lead to an address over the very table
 * being linked (TestApp catalog/linkowner -> linkover); what that address checks reads columns the
 * owner has not reached yet. So the whole load links first, and only then checks.
 */
public class LoadOrderTests
{
    [Theory]
    [InlineData("linkowner")]
    [InlineData("linkover")]
    public async Task A_cycle_through_a_shared_table_loads_from_either_end(String table)
    {
        var endpoint = (NormalEndpointMetadata)await TestHost.GetService<DatabaseMetadataProvider>()
            .GetEndpointAsync(null, "catalog", table);

        var rate = endpoint.Storage.AllColumns().First(c => c.Name == "Rate");
        Assert.Equal("/enum/vatrate", rate.RefTableCheck.Path);
    }
}

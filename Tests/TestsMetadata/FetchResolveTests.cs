// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using A2v10.Infrastructure;
using A2v10.Metadata;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* The references a fetch carries (inherit) are resolved by the one resolve, RefMapBuilder.GenerateResolves:
 * the object a selector hands to a row has the shape a load hands to it. A copy kept in the fetch sent no
 * Role of a state, no Done/$Url/$Icon of a document, and read a document's table instead of its view.
 */
public class FetchResolveTests
{
    static async Task<(SqlBuilder Builder, TableMetadata Table)> BuilderAsync()
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var platformUrl = new PlatformUrl("_page/document/waybillin/index/0");
        var endpoint = await provider.GetNormalEndpointAsync(null, "document", "waybillin");
        var builder = new SqlBuilder(new BuilderDescriptor()
        {
            Endpoint = endpoint,
            PlatformUrl = platformUrl,
            PlatformId = await provider.GetPlatformIdAsync(null),
            UseGrants = false,
        }, TestHost.Services);
        return (builder, endpoint.Storage);
    }

    [Fact]
    public async Task A_fetched_reference_has_the_shape_a_load_gives_it()
    {
        var (builder, table) = await BuilderAsync();
        dynamic prms = new ExpandoObject();
        prms.inherit = "State,BaseOrder,Shipment";

        String sql = builder.FetchSqlText(prms);

        var state = table.AllColumns().First(c => c.Name == "State").RefTableCheck.Storage;
        var order = table.AllColumns().First(c => c.Name == "BaseOrder").RefTableCheck.Storage;

        Assert.Contains($"[Name!!Name] = a.[{state.Presentation}]{SqlBuilder.RefFields(state, "a")}", sql);
        Assert.Contains($"[{Constants.FieldNames.Role}]", sql);
        Assert.Contains($"[Name!!Name] = a.[{order.Presentation}]{SqlBuilder.RefFields(order, "a")}", sql);
        Assert.Contains($"from {order.RefSourceName} a inner join T", sql);
        foreach (var member in Constants.FieldNames.RefViewMembers)
            Assert.Contains($"a.[{member}]", sql);
    }
}

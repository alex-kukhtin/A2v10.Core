// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using Newtonsoft.Json;

using A2v10.Infrastructure;
using A2v10.Platform.Web;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* A menu entry names an endpoint and the menu adds the rest; everywhere else an address is full, and
 * one without an action says so.
 */
public class MenuEndpointTests
{
    static JsonMenu Read(String json) => JsonConvert.DeserializeObject<JsonMenu>(json, A2v10.Platform.Web.JsonHelpers.StandardSerializerSettings)!;

    [Theory]
    [InlineData("""{ "endpoint": "/catalog/new" }""", "/catalog/new")]
    [InlineData("""{ "url": "/catalog/old" }""", "/catalog/old")]
    [InlineData("""{ "endpoint": "/catalog/new", "url": "/catalog/old" }""", "/catalog/new")]
    [InlineData("""{ "url": "/catalog/old", "endpoint": "/catalog/new" }""", "/catalog/new")]
    public void A_menu_written_with_the_former_key_still_reads(String json, String endpoint)
    {
        Assert.Equal(endpoint, Read(json).Endpoint);
    }

    [Fact]
    public void The_menu_adds_the_action_and_the_id()
    {
        var leaf = Read("""{ "Title": "t", "url": "/catalog/agent", "create": true }""");
        var root = new JsonMenuRoot() { Menu = [new JsonMenu() { Title = "s", Items = [new JsonMenu() { Title = "g", Items = [leaf] }] }] };

        var json = JsonMenuRoot.ConvertToPlatformMenu(root, TestHost.GetService<ILocalizer>());

        Assert.Contains("page:/catalog/agent/index/0", json);
        Assert.Contains("dialog:/catalog/agent/edit/new", json);
    }

    [Theory]
    [InlineData("_page/catalog/agent", "/catalog/agent")]
    [InlineData("_page/document/0", "/document")]
    public async Task An_endpoint_is_not_a_page_address(String url, String address)
    {
        var ex = await Assert.ThrowsAsync<ModelJsonException>(
            () => TestHost.GetService<IModelJsonReader>().GetViewAsync(new PlatformUrl(url)));

        Assert.Contains($"'{address}' has no action", ex.Message);
    }
}

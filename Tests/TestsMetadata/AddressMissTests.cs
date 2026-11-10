// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* A folder is served by its model.json, then by its metadata.json. With neither the answer says so:
 * the metadata layer used to take every miss as its own and fail in its own terms.
 */
public class AddressMissTests
{
    static Task<IModelView> View(String url) =>
        TestHost.GetService<IModelJsonReader>().GetViewAsync(new PlatformUrl(url));

    [Theory]
    [InlineData("_page/catalog/nosuch/index/0", "catalog/nosuch")]
    [InlineData("_dialog/catalog/agent/edit/0/0", "catalog/agent/edit")]
    [InlineData("_dialog/catalog/agent/0", "catalog")]
    public async Task A_folder_with_neither_file_says_so(String url, String folder)
    {
        var ex = await Assert.ThrowsAsync<ModelJsonException>(() => View(url));

        Assert.Equal($"Neither model.json nor metadata.json in '{folder}'", ex.Message);
    }

    [Fact]
    public async Task A_folder_with_a_metadata_file_is_served() =>
        Assert.NotNull(await View("_page/catalog/agent/index/0"));

    [Fact]
    public async Task An_address_of_the_platform_needs_no_file() =>
        Assert.NotNull(await View("_dialog/tag/settings/0"));
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* A second address over a table ('storage') does not write into it: the table's columns are linked once,
 * by its owner. And the folders of a record are reached through the address that reads it - an answer
 * that differs by endpoint, so the column of the shared table does not hold it (FoldersTarget).
 * TestApp: /catalog/goods over /catalog/item, which has folders.
 */
public class FolderTargetTests
{
    static async Task<NormalEndpointMetadata> EndpointAsync(String table) =>
        await TestHost.GetService<DatabaseMetadataProvider>().GetNormalEndpointAsync(null, "catalog", table);

    static TableColumn FolderOf(NormalEndpointMetadata endpoint) =>
        endpoint.Storage.AllColumns().First(c => c.Type == ColumnType.Folder);

    // a guard: with the cache shared by the run, goods may already be loaded, and then nothing would relink
    [Fact]
    public async Task A_second_address_does_not_relink_the_owners_table()
    {
        var item = await EndpointAsync("item");
        var target = FolderOf(item).RefTable;

        var goods = await EndpointAsync("goods");

        Assert.Same(item.Storage, goods.Storage);
        Assert.Same(target, FolderOf(item).RefTable);
    }

    [Fact]
    public async Task Folders_have_no_address_of_their_own()
    {
        var folder = FolderOf(await EndpointAsync("goods"));

        Assert.IsType<FoldersTarget>(folder.RefTable);
        Assert.Throws<NotSupportedException>(() => folder.RefTableCheck.Path);
    }

    [Theory]
    [InlineData("item")]
    [InlineData("goods")]
    public async Task The_folders_of_a_record_are_picked_at_the_address_that_reads_it(String table)
    {
        // both loaded, in any order the cache saw them: the answer must not depend on who came last
        await EndpointAsync("goods");
        await EndpointAsync("item");
        var endpoint = await EndpointAsync(table);
        var descriptor = new BuilderDescriptor()
        {
            Endpoint = endpoint,
            PlatformUrl = endpoint.PlatformUrl("edit"),
            PlatformId = await TestHost.GetService<DatabaseMetadataProvider>().GetPlatformIdAsync(null),
            UseGrants = false
        };

        var xaml = XamlTextBulder.GetXaml(new XamlBuilder(descriptor.Endpoint, descriptor.PlatformUrl).CreateXamlContainer("edit"));

        Assert.Contains($"Url=\"/catalog/{table}\" Folder=\"True\"", xaml);
    }
}

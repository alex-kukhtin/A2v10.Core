// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Metadata;
using A2v10.Services;
using A2v10.Xaml;

namespace A2v10.Metadata.Tests;

/* A page names its tab (a2-document-title) only when it has a Title. Opened from the menu the tab
 * takes the menu's caption; opened otherwise - a link, a reload, a card from the list - it stayed
 * blank. So every generated page carries one: a list its collection (a document - its own name), a
 * card its record.
 */
public class PageTitleTests
{
    static async Task<NormalEndpointMetadata> LoadAsync(String schema, String table) =>
        (NormalEndpointMetadata)await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table);

    static UIElement Build(NormalEndpointMetadata endpoint, String action) =>
        Built(new XamlBuilder(endpoint, endpoint.PlatformUrl(action)).CreateXamlContainer(action));

    // a builder's 'Bindings' are set at the end of initialization, as the reader would end it
    static UIElement Built(UIElement element)
    {
        element.InitComplete();
        return element;
    }

    [Fact]
    public async Task Every_generated_page_has_a_title()
    {
        var provider = TestHost.GetService<DatabaseMetadataProvider>();
        var validator = new EndpointValidator(provider, TestHost.GetService<IAppCodeProvider>(), TestHost.Services);

        var pages = 0;
        var untitled = new List<String>();
        foreach (var file in TestHost.GetService<IAppCodeProvider>().EnumerateAllFilesRecursive("", "metadata.json"))
        {
            var folder = Path.GetDirectoryName(file)!.NormalizeSlash();
            var (schema, table) = DatabaseMetadataProvider.ParsePath(folder);
            if (await provider.GetEndpointAsync(null, schema, table) is not NormalEndpointMetadata { Declaration.BakedForms.Count: > 0 } endpoint)
                continue;
            // TestApp keeps negative fixtures for the validator's own tests
            if ((await validator.ValidateAsync(folder)).Error != null)
                continue;
            foreach (var (action, url) in XamlBeamTests.ScreensOf(endpoint))
            {
                if (Built(new XamlBuilder(endpoint, url).CreateXamlContainer(action)) is not Page page)
                    continue;
                pages++;
                if (String.IsNullOrEmpty(page.Title) && page.GetBinding(nameof(Page.Title)) == null)
                    untitled.Add($"{endpoint.Path} {action}");
            }
        }

        Assert.True(pages > 0, "no page was built");
        Assert.True(untitled.Count == 0, $"{untitled.Count} of {pages} pages have no title:\n{String.Join("\n", untitled)}");
    }

    // documents over one table share its model: the list and the card are named by the document
    [Fact]
    public async Task A_document_is_named_by_its_own_name()
    {
        var endpoint = await LoadAsync("document", "waybillout");

        Assert.Equal("@[Documents.waybillout]", ((Page)Build(endpoint, Constants.Actions.Index)).Title);
        var card = (Page)Build(endpoint, Constants.Actions.Edit);
        Assert.Equal("@[Operation.waybillout] [{0}]", card.GetBinding(nameof(Page.Title))?.Format);
    }

    [Fact]
    public async Task A_catalog_list_is_named_by_its_collection()
    {
        var endpoint = await LoadAsync("catalog", "agent");

        Assert.Equal($"@[{endpoint.Storage.CollectionName}]", ((Page)Build(endpoint, Constants.Actions.Index)).Title);
    }

    [Fact]
    public void The_users_screen_has_a_title() =>
        Assert.Equal("@[Users]", ((Page)new XamlUsersBuilder("/admin/user").IndexPage()).Title);
}

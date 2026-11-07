// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Infrastructure;
using A2v10.Metadata;
using A2v10.Xaml;

namespace A2v10.Metadata.Tests;

/* How the card opens is the endpoint's 'forms.edit.is', not the table's kind. A catalog written
 * 'page' built a page that the list opened in a dialog, and 'Save and close' had nothing to close.
 * TestApp: catalog/project is a page, catalog/agent keeps the default dialog.
 */
public class EditPageTests
{
    static async Task<NormalEndpointMetadata> LoadAsync(String table) =>
        (NormalEndpointMetadata)await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, "catalog", table);

    static async Task<String> IndexAsync(String table) =>
        (await TestHost.GetService<EndpointMaterializer>()
            .MaterializeAsync($"/catalog/{table}", "index", MaterializeWhat.View)).Files[0].Text;

    [Fact]
    public async Task A_catalog_written_page_opens_its_card_in_a_tab()
    {
        var endpoint = await LoadAsync("project");
        var view = await IndexAsync("project");

        Assert.True(endpoint.Declaration.EditIsPage);
        Assert.Equal(UrlKind.Page, endpoint.PlatformUrl("edit").Kind);
        Assert.Contains("{BindCmd Open, Url='/catalog/project/edit', Argument='new'}", view);
        Assert.Contains("{BindCmd OpenSelected, Url='/catalog/project/edit'", view);
        Assert.DoesNotContain("{BindCmd Dialog", view);
    }

    [Fact]
    public async Task A_catalog_by_default_opens_its_card_in_a_dialog()
    {
        var endpoint = await LoadAsync("agent");

        Assert.False(endpoint.Declaration.EditIsPage);
        Assert.Equal(UrlKind.Dialog, endpoint.PlatformUrl("edit").Kind);
    }
}

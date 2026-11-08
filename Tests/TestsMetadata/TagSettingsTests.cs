// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;
using A2v10.Services;

namespace A2v10.Metadata.Tests;

/* The tag dictionary is the model's; the dialog is opened from a screen, and the right is that screen's.
 * TestApp: catalog/agent has the trait, catalog/agentover is a second address over it, catalog/item has none.
 */
public class TagSettingsTests
{
    static DatabaseMetadataProvider Provider => TestHost.GetService<DatabaseMetadataProvider>();

    static async Task<TagEndpointBuilder> BuilderOf(String query, Boolean useGrants = true)
    {
        var platformUrl = new PlatformUrl($"_dialog/tag/settings/0?{query}");
        var (schema, table) = DatabaseMetadataProvider.ParsePath(platformUrl.LocalPath);
        return new TagEndpointBuilder(TestHost.Services, (TagEndpointMetadata)await Provider.GetEndpointAsync(null, schema, table),
            platformUrl, null, await Provider.GetPlatformIdAsync(null), useGrants);
    }

    static async Task<SqlBuilderTags> SqlOf(Boolean useGrants) =>
        new(TestHost.Services, await Provider.GetPlatformIdAsync(null), useGrants);

    static Task<NormalEndpointMetadata> ScreenOf(String name) => Provider.GetNormalEndpointAsync(null, "catalog", name);

    [Theory]
    [InlineData("agent")]
    [InlineData("agentover")]
    public async Task The_model_comes_off_the_screen_and_each_address_answers_for_itself(String name)
    {
        var screen = await (await BuilderOf($"Base=/catalog/{name}")).ScreenAsync();

        Assert.Equal($"/catalog/{name}", screen.Path);
        Assert.Equal("Agent", screen.Storage.Model);
    }

    [Theory]
    [InlineData("Base=/catalog/item", "no 'Tags' trait")]
    [InlineData("For=Agent", "'?Base=' is missing")]
    public async Task A_screen_without_tags_and_a_url_without_a_screen_are_refused(String query, String message)
    {
        var builder = await BuilderOf(query);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(builder.ScreenAsync);

        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public async Task The_open_needs_edit_of_the_screen()
    {
        var sql = (await SqlOf(useGrants: true)).LoadBatch(await ScreenOf("agentover"));

        var gate = sql.IndexOf("@Url = N'/catalog/agentover', @Flag = N'Edit';");
        Assert.True(gate > sql.IndexOf("set transaction isolation level"), "the gate is not after the 'set's");
        Assert.True(gate < sql.IndexOf("select [Tags!"), "the gate is not ahead of the read");
    }

    [Fact]
    public async Task The_save_needs_edit_of_the_screen()
    {
        var sql = (await SqlOf(useGrants: true)).SaveBatch(await ScreenOf("agentover"));

        var gate = sql.IndexOf("@Url = N'/catalog/agentover', @Flag = N'Edit';");
        Assert.True(gate > sql.IndexOf("set transaction isolation level"), "the gate is not after the 'set's");
        Assert.True(gate < sql.IndexOf("merge "), "the gate is not ahead of the merge");
    }

    [Fact]
    public async Task Without_grants_there_is_no_gate()
    {
        var sql = await SqlOf(useGrants: false);
        var screen = await ScreenOf("agent");

        Assert.DoesNotContain("Permission.Check", sql.LoadBatch(screen));
        Assert.DoesNotContain("Permission.Check", sql.SaveBatch(screen));
    }

    // the dictionary stays the model's for the templates: tagsSaved compares Params.For with the model
    [Fact]
    public async Task The_answer_still_names_the_model()
    {
        var sql = await SqlOf(useGrants: false);
        var screen = await ScreenOf("agentover");

        Assert.Contains("[For] = N'Agent'", sql.LoadBatch(screen));
        Assert.Contains("[For] = N'Agent'", sql.SaveBatch(screen));
    }

    // unset, the client's default shows 'Settings' in English in every language
    [Fact]
    public async Task The_settings_button_is_localized()
    {
        var view = (await TestHost.GetService<EndpointMaterializer>()
            .MaterializeAsync("/catalog/agent", "edit", MaterializeWhat.View)).Files[0].Text;

        Assert.Contains("SettingsText=\"@[Tag.Settings]\"", view);
    }

    [Theory]
    [InlineData("agent")]
    [InlineData("agentover")]
    public async Task The_card_opens_the_dialog_from_its_own_address(String name)
    {
        var view = (await TestHost.GetService<EndpointMaterializer>()
            .MaterializeAsync($"/catalog/{name}", "edit", MaterializeWhat.View)).Files[0].Text;

        Assert.Contains($"/tag/settings?Base=/catalog/{name}'", view);
    }
}

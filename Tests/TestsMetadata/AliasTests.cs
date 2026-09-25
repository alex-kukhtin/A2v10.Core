// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* app.json 'aliases': sale/ is one more folder of kind document. TestApp declares it and lays
 * sale/invoice beside document/waybillin.
 */
public class AliasTests
{
    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(
            await TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table));

    [Fact]
    public async Task An_operation_under_an_alias_is_addressed_by_its_folder_and_is_of_the_kind()
    {
        var operation = await LoadNormalAsync("sale", "invoice");
        var storage = await LoadNormalAsync("document", String.Empty);

        Assert.Equal(EndpointKind.Document, operation.Kind);
        Assert.Equal("/sale/invoice", operation.Path);
        Assert.Same(storage.Storage, operation.Storage);
        // the Id is the last segment: the alias is where the file lies, not a part of the name
        Assert.Equal("invoice", operation.DocumentOperation());
    }

    [Fact]
    public void A_folder_nobody_aliased_answers_as_it_is()
    {
        var folders = KindFolders.From(new Dictionary<String, String[]> { ["document"] = ["sale"] });

        Assert.Equal("document", folders.KindOf("sale"));
        Assert.Equal("document", folders.KindOf("document"));
        Assert.Equal("catalog", folders.KindOf("catalog"));
    }

    [Fact]
    public void A_key_that_is_not_a_kind_is_refused() =>
        Assert.Throws<InvalidOperationException>(() =>
            KindFolders.From(new Dictionary<String, String[]> { ["documnt"] = ["sale"] }));

    [Theory]
    [InlineData("catalog")]
    [InlineData("operation")]
    [InlineData("autonum")]
    [InlineData("patches")]
    public void An_alias_that_is_a_platform_name_is_refused(String alias) =>
        Assert.Throws<InvalidOperationException>(() =>
            KindFolders.From(new Dictionary<String, String[]> { ["document"] = [alias] }));

    [Fact]
    public void An_alias_on_dollar_is_refused_as_a_module_segment() =>
        Assert.Throws<InvalidOperationException>(() =>
            KindFolders.From(new Dictionary<String, String[]> { ["document"] = ["$sale"] }));

    [Fact]
    public void One_folder_under_two_kinds_is_refused() =>
        Assert.Throws<InvalidOperationException>(() =>
            KindFolders.From(new Dictionary<String, String[]> { ["document"] = ["sale"], ["catalog"] = ["sale"] }));
}

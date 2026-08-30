// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The floor: the app really loads, and the two things this session decided about it hold.
 * See Platform/A2v10.Metadata/TESTS.md, layer C.
 */
public class EndpointLoadTests
{
    const String Document = "document";

    static Task<EndpointMetadata> LoadAsync(String schema, String table) =>
        TestHost.GetService<DatabaseMetadataProvider>().GetEndpointAsync(null, schema, table);

    static async Task<NormalEndpointMetadata> LoadNormalAsync(String schema, String table) =>
        Assert.IsType<NormalEndpointMetadata>(await LoadAsync(schema, table));

    [Fact]
    public async Task Operation_and_its_storage_share_one_shape_instance()
    {
        var operation = await LoadNormalAsync(Document, "waybillin");
        var storage = await LoadNormalAsync(Document, String.Empty);

        // ReferenceEquals, not equality: the whole storage cache stands on this
        Assert.Same(storage.Storage, operation.Storage);
    }

    [Fact]
    public async Task PrintForms_are_the_operations_own_and_not_the_storages()
    {
        var operation = await LoadNormalAsync(Document, "waybillin");
        var storage = await LoadNormalAsync(Document, String.Empty);

        Assert.Empty(storage.Declaration.PrintForms);

        var form = Assert.Single(operation.Declaration.PrintForms);
        Assert.Equal("print/printform1", form.Path);
        Assert.False(String.IsNullOrEmpty(form.Title));
    }
}

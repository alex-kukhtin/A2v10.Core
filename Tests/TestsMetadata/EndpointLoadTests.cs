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

    /* The link back to the header is named for what it POINTS AT - the master's Model - which is
     * the one naming rule every link column here follows, refs included. It is therefore not a
     * constant any more, and the only way back to it is the column's type: this pins both halves,
     * because a generator that kept a literal name would still pass the first assertion alone.
     */
    [Fact]
    public async Task The_link_to_the_master_is_named_after_the_masters_model()
    {
        var doc = (await LoadNormalAsync(Document, String.Empty)).Storage;
        var rows = doc.Details["Rows"];

        Assert.Equal(doc.Model, rows.MasterField);

        var master = Assert.Single(rows.AllColumns(c => c.Type == ColumnType.Master));
        Assert.Equal(rows.MasterField, master.Name);
    }

    /* The second satellite that hangs under a record, and the reason the rule is a rule rather
     * than a detail of the details tables: it is built on the fly, by another factory, and would
     * be exactly the place for the old fixed name to survive.
     */
    [Fact]
    public async Task Tag_entries_are_named_by_the_same_rule()
    {
        var doc = (await LoadNormalAsync(Document, String.Empty)).Storage;
        var entries = TableMetadataDefaults.CreateTagEntriesTable(doc);

        Assert.Equal(doc.Model, entries.MasterField);

        var master = Assert.Single(entries.AllColumns(c => c.Type == ColumnType.Master));
        Assert.Equal(entries.MasterField, master.Name);
    }
}

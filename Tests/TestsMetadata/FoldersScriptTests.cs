// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The folders of a catalog: a table of their own, a closed set of columns, and a link from the
 * owner that the database enforces. The table has no address, so nothing resolves it - the deploy
 * walk alone brings it, and the seed must name the same target as the foreign key, or the hash
 * would not see the link. No database. See TESTS.md, layer D.
 */
public class FoldersScriptTests
{
    static async Task<TableMetadata> ItemAsync() =>
        ((NormalEndpointMetadata)await TestHost.GetService<DatabaseMetadataProvider>()
            .GetEndpointAsync(null, "catalog", "item")).Storage;

    // a name, a tree and a withdrawal - nothing the application could read
    [Fact]
    public async Task A_folder_carries_a_name_in_a_tree_and_nothing_else()
    {
        var folders = TableMetadataDefaults.CreateFoldersTable(await ItemAsync());

        Assert.Equal("cat.[Item$Folders]", folders.SqlTableName);
        Assert.Equal(
            ["Id", "Void", "Name", "Memo", "Parent",
                "UserCreated", "UtcDateCreated", "UserModified", "UtcDateModified"],
            folders.AllColumns().Select(c => c.Name));
    }

    [Fact]
    public async Task The_owner_points_at_its_folders_and_a_folder_at_its_parent()
    {
        var sql = SqlDbGenerator.CreateForeignKeysScript([await ItemAsync()]);

        Assert.Contains("foreign key ([Folder]) references cat.[Item$Folders]([Id])", sql);
        Assert.Contains("foreign key ([Parent]) references cat.[Item$Folders]([Id])", sql);
    }

    /* All (the view of the catalog) and Root (the place of what lies in no folder) are reserved values
     * of the domain, spelled per base. Each has to be an id of that base - SQL casts it to it - and
     * the two must not meet; the root, a place the list filters by, must not meet the empty reference.
     */
    [Theory]
    [InlineData("bigint")]
    [InlineData("int")]
    [InlineData("uniqueidentifier")]
    public void The_reserved_folder_nodes_are_ids_of_the_base(String sqlName)
    {
        var id = AppPlatformId.FromSqlName(sqlName);

        var all = id.ParseId(id.All);
        var root = id.ParseId(id.Root);

        Assert.NotNull(all);
        Assert.NotNull(root);
        Assert.NotEqual(all, root);
        Assert.False(AppPlatformId.IsEmpty(root));
    }

    // the trait says it, not the file: a new element starts in the folder the url that opens it names
    [Fact]
    public async Task A_new_element_starts_in_the_folder_the_url_names()
    {
        var item = (NormalEndpointMetadata)await TestHost.GetService<DatabaseMetadataProvider>()
            .GetEndpointAsync(null, "catalog", "item");

        var folder = item.AllInitials()["Folder"];

        Assert.Equal(InitialSource.Query, folder.Source);
        Assert.Equal("Folder", folder.Value);
    }

    [Fact]
    public async Task The_seed_knows_the_folders_table_and_the_link_to_it()
    {
        var seed = await SqlDbGenerator.GenerateMetadataSeedAsync([await ItemAsync()]);
        Assert.NotNull(seed);

        var lines = seed.Split('\n');
        Assert.Contains(lines, l => l.Contains("(N'cat', N'Item$Folders', "));
        var folder = Assert.Single(lines, l => l.Contains("N'Items', N'Folder', "));
        Assert.Contains("N'cat', N'Item$Folders'", folder);
    }
}

// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using A2v10.Metadata;

namespace A2v10.Metadata.Tests;

/* The view a reference to a document reads, and the registry column its address comes from. No
 * database: the generator turns metadata into text. See TESTS.md, layer D.
 */
public class RefViewScriptTests
{
    static async Task<TableMetadata> StorageAsync(String schema, String table) =>
        ((NormalEndpointMetadata)await TestHost.GetService<DatabaseMetadataProvider>()
            .GetEndpointAsync(null, schema, table)).Storage;

    // several documents share the table, so the row's operation says whose it is and where it opens
    [Fact]
    public async Task A_shared_storage_takes_the_address_from_the_operation()
    {
        var sql = SqlDbGenerator.CreateRefViewsScript([await StorageAsync("document", String.Empty)]);

        Assert.Contains("create or alter view doc.[StockDocuments$Ref] as", sql);
        Assert.Contains("select a.*, [$Url] = o.[Path] + N'/edit', [$Icon] = case when a.[Done] = 1 then N'success-outline-green' else N'warning-outline-yellow' end", sql);
        Assert.Contains("left join doc.[Operations] o on o.[Id] = a.[Operation];", sql);
    }

    // one document, one address - the join would ask the registry for what the table already says
    [Fact]
    public async Task A_document_without_operations_has_its_own_address()
    {
        var sql = SqlDbGenerator.CreateRefViewsScript([await StorageAsync("document", "order")]);

        Assert.Contains("create or alter view doc.[Orders$Ref] as", sql);
        Assert.Contains("select a.*, [$Url] = N'/document/order/edit', [$Icon] = case", sql);
        Assert.DoesNotContain("join", sql);
    }

    [Fact]
    public async Task Nothing_but_a_document_gets_a_view() =>
        Assert.Empty(SqlDbGenerator.CreateRefViewsScript([await StorageAsync("catalog", "agent")]));

    /* Why the registry keeps the address beside the name: under an alias they differ, and nothing
     * rewrites /document/invoice to /sale/invoice.
     */
    [Fact]
    public async Task The_registry_keeps_the_address_the_file_lies_at()
    {
        var (tables, _) = await TestHost.GetService<DatabaseMetadataProvider>().AllElementsMetadata(null);
        var operations = tables.Single(t => t.Kind == TableKind.Operation).Rows;

        var invoice = operations.Single(o => o.Id == "invoice");
        Assert.Equal("invoice", invoice.Values[Constants.FieldNames.Document]);
        Assert.Equal("/sale/invoice", invoice.Values[Constants.FieldNames.Path]);
        Assert.Equal("/document/receipt", operations.Single(o => o.Id == "receipt.supplier").Values[Constants.FieldNames.Path]);
    }
}
